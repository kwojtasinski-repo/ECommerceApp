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
            services.AddScoped<GuestAccessPrincipalValidator>();

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
                    // The events delegate runs at ConfigureServices time — no request-scoped service
                    // exists yet to constructor-inject, so resolving GuestAccessPrincipalValidator per
                    // request via RequestServices is the standard ASP.NET Core pattern for cookie events.
                    // The actual revocation logic lives in (and is unit-tested on) that class, not here.
                    options.Events.OnValidatePrincipal = context =>
                        context.HttpContext.RequestServices
                            .GetRequiredService<GuestAccessPrincipalValidator>()
                            .ValidateAsync(context);
                })
                .AddGoogle(options =>
            {
                IConfigurationSection configurationSection = Configuration.GetSection("Authentication:Google");
                options.ClientId = configurationSection["ClientId"];
                options.ClientSecret = configurationSection["ClientSecret"];
            });
            services.AddAuthorization(options =>
            {
                // Deliberately NOT widened to include GuestAccess: this is the app-wide default for
                // every bare [Authorize] (every Razor Pages area/folder convention too, notably
                // Identity/Manage via AddDefaultIdentity) — a GuestAccess ticket is not a real
                // ApplicationUser, so it must stay out of anything that doesn't explicitly opt in.
                options.DefaultPolicy = new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
                options.AddPolicy("OrderAccess", policy =>
                    policy.Requirements.Add(new OrderAccessRequirement()));
                // The explicit opt-in for the handful of controllers (checkout, orders, payments,
                // refunds, order items) that must accept an anonymous-turned-GuestAccess caller as well
                // as a real signed-in customer. Everything else in the app stays ApplicationScheme-only
                // by default — see DefaultPolicy above.
                options.AddPolicy("CustomerOrGuest", policy =>
                    policy.AddAuthenticationSchemes(
                            IdentityConstants.ApplicationScheme,
                            GuestAccessDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser());
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

                // Per-IP limits on the two guest checkout actions that actually grow DB footprint for an
                // anonymous caller: AddToCart is also where ShopperIdentityResolver first mints a new
                // GuestSession cookie for a brand-new visitor (guest-cookie issuance has no dedicated
                // endpoint of its own — it is a side effect of this action, not a separate one), and
                // PlaceOrder POST is where a UserProfile + Order actually get created. Attached via
                // [EnableRateLimiting("...")] directly on the actions (not GlobalLimiter path/route
                // matching) — simplest correct option for a plain per-IP check with no per-resource key.
                options.AddPolicy("GuestCheckoutAddToCart", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 30,
                            Window = TimeSpan.FromMinutes(10),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
                options.AddPolicy("GuestCheckoutPlaceOrder", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(10),
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
