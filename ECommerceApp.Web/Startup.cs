using System.Collections.Generic;
using System;
using ECommerceApp.Web.Areas.Catalog.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ECommerceApp.Infrastructure;
using ECommerceApp.Application;
using Microsoft.AspNetCore.Localization;
using ECommerceApp.Web.Filters;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Presale.Checkout.Options;
using ECommerceApp.Web.Areas.Presale.Services;
using ECommerceApp.Web.Services;
using ECommerceApp.Infrastructure.Supporting.Communication;
using ECommerceApp.Web.Areas.Presale.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using System.Globalization;

namespace ECommerceApp.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ICartRequirements>(new CartRequirements(CheckoutOptions.MaxWebQuantityPerOrderLine));
            services.AddSingleton<JsVersionProvider>();
            services.Configure<CatalogOptions>(Configuration.GetSection(CatalogOptions.SectionName));
            services.AddApplication();
            services.AddInfrastructure(Configuration);
            services.AddScoped<IShopperIdentityResolver, ShopperIdentityResolver>();
            services.AddScoped<IOrderAccessAuthorizer, OrderAccessAuthorizer>();

            services.AddControllersWithViews(options =>
            {
                options.Filters.Add<FluentValidationModelStateFilter>(); // must run first: populates ModelState from FV
                options.Filters.Add(new ModelStateFilter());             // then short-circuits on invalid ModelState
            });
            services.AddRazorPages();

            services.Configure<IdentityOptions>(opt =>
            {
                opt.Password.RequireDigit = true;
                opt.Password.RequireLowercase = true;
                opt.Password.RequiredLength = 8;

                opt.SignIn.RequireConfirmedEmail = false;
                opt.User.RequireUniqueEmail = true;
            });

            services.AddAuthentication()
                .AddCookie(GuestAccessDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = GuestAccessDefaults.CookieName;
                    options.Cookie.Path = "/";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.ExpireTimeSpan = TimeSpan.FromDays(30);
                    options.SlidingExpiration = true;
                    options.Events.OnValidatePrincipal = async context =>
                    {
                        var validatedAt = context.Principal?.FindFirst(GuestAccessDefaults.ValidatedAtClaim)?.Value;
                        if (DateTimeOffset.TryParse(validatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastValidated)
                            && DateTimeOffset.UtcNow - lastValidated < TimeSpan.FromMinutes(5))
                            return;

                        var orderIdValue = context.Principal?.FindFirst(GuestAccessDefaults.OrderIdClaim)?.Value;
                        var token = context.Principal?.FindFirst(GuestAccessDefaults.BackingTokenClaim)?.Value;
                        if (!int.TryParse(orderIdValue, out var orderId) || string.IsNullOrWhiteSpace(token))
                        {
                            context.RejectPrincipal();
                            return;
                        }

                        var orderAccessService = context.HttpContext.RequestServices
                            .GetRequiredService<IOrderAccessService>();
                        if (!await orderAccessService.HasAccessAsync(orderId, token, context.HttpContext.RequestAborted))
                        {
                            context.RejectPrincipal();
                            return;
                        }

                        var identity = context.Principal.Identity as System.Security.Claims.ClaimsIdentity;
                        var existingValidationClaim = identity?.FindFirst(GuestAccessDefaults.ValidatedAtClaim);
                        if (existingValidationClaim is not null)
                            identity.RemoveClaim(existingValidationClaim);
                        identity?.AddClaim(new System.Security.Claims.Claim(
                                GuestAccessDefaults.ValidatedAtClaim,
                                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
                        context.ShouldRenew = true;
                    };
                })
                .AddGoogle(options =>
            {
                IConfigurationSection configurationSection = Configuration.GetSection("Authentication:Google");
                options.ClientId = configurationSection["ClientId"];
                options.ClientSecret = configurationSection["ClientSecret"];
            });
            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder(
                        IdentityConstants.ApplicationScheme,
                        GuestAccessDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
                options.AddPolicy("OrderAccess", policy =>
                    policy.Requirements.Add(new OrderAccessRequirement()));
            });
            services.AddScoped<IAuthorizationHandler, OrderAccessAuthorizationHandler>();
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, _) =>
                {
                    context.HttpContext.Response.Headers.RetryAfter = "900";
                    return ValueTask.CompletedTask;
                };

                // The per-IP limit is a GlobalLimiter matched by route values (action/controller), not by
                // URL path suffix: this app's default {area}/{controller}/{action}/{id} route makes
                // RequestOrderAccess resolve to ".../RequestOrderAccess/{id}" — a URL that never ends with
                // a literal "/RequestAccess" suffix, so a path-suffix-matched limiter would silently never
                // fire. UseRateLimiter() runs after UseRouting(), so route values are already populated
                // here (the same reason the per-OrderId policy below can already key on RouteValues["id"]).
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var isRequestOrderAccess =
                        string.Equals(httpContext.Request.RouteValues["controller"] as string, "Checkout", StringComparison.Ordinal) &&
                        string.Equals(httpContext.Request.RouteValues["action"] as string, "RequestOrderAccess", StringComparison.Ordinal);
                    if (!isRequestOrderAccess)
                        return RateLimitPartition.GetNoLimiter("not-order-access");

                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(
                        ip,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(10),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                });
                // Applied explicitly via [EnableRateLimiting("OrderAccessByOrderId")] on RequestOrderAccess.
                options.AddPolicy("OrderAccessByOrderId", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.Request.RouteValues["id"]?.ToString() ?? "unknown-order",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(15),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });
            services.AddDatabaseDeveloperPageExceptionFilter();
            services.AddWebCaching(Configuration);
            services.AddTusServices(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IOptions<CatalogOptions> catalogOptions)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseOutputCache(); // must be after UseStaticFiles, before UseRouting

            var defaultCulture = new CultureInfo("pl-PL");
            var localizationOptions = new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(defaultCulture),
                SupportedCultures = new List<CultureInfo> { defaultCulture },
                SupportedUICultures = new List<CultureInfo> { defaultCulture }
            };
            app.UseRequestLocalization(localizationOptions);
            app.UseRouting();
            app.UseRateLimiter();

            app.UseAuthentication();
            app.UseAuthorization();

            if (catalogOptions.Value.UseTusUpload)
                app.UseTusUpload();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "areas",
                    pattern: "{area:exists}/{controller}/{action=Index}/{id?}");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
                endpoints.MapCommunicationHubs();
            });
        }
    }
}
