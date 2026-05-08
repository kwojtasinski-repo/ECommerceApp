using System.Collections.Generic;
using System.Text;
using ECommerceApp.API.Filters;
using ECommerceApp.API.Options;
using ECommerceApp.Application;
using ECommerceApp.Application.Middlewares;
using ECommerceApp.Application.Permissions;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace ECommerceApp.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = Configuration["Jwt:Issuer"],
                        ValidAudience = Configuration["Jwt:Issuer"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]))
                    };
                });
            services.AddAuthorizationBuilder()
                .AddPolicy(ApiPolicies.TrustedApiUser, policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireAssertion(ctx =>
                              ctx.User.HasClaim("api:purchase", "true") ||
                              ctx.User.IsInRole(UserPermissions.Roles.Service) ||
                              ctx.User.IsInRole(UserPermissions.Roles.Manager) ||
                              ctx.User.IsInRole(UserPermissions.Roles.Administrator)));
            services.Configure<WebOptions>(Configuration.GetSection("WebOptions"));
            services.AddScoped<MaxApiQuantityFilter>();
            services.AddSingleton<ICartRequirements>(new CartRequirements(ApiPurchaseOptions.MaxQuantityPerOrderLine));
            services.AddApplication();
            services.AddInfrastructure(Configuration);
            services.AddControllers(options =>
            {
                options.Filters.Add<FluentValidationFilter>();
            }).AddNewtonsoftJson(options =>
                     options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);

            services.AddSwaggerGen(setup =>
            {
                setup.SwaggerDoc("v1", new OpenApiInfo { Title = "Ecommerce.API", Version = "v1" });

                // Include 'SecurityScheme' to use JWT Authentication
                var jwtSecurityScheme = new OpenApiSecurityScheme
                {
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    BearerFormat = "JWT",
                    Name = "JWT Authentication",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Description = "Put **_ONLY_** your JWT Bearer token on textbox below!",
                };

                setup.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtSecurityScheme);
                setup.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme),
                        new List<string>()
                    }
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ecommerce.API v1");
                    c.OAuthClientId("swagger");
                    c.OAuth2RedirectUrl(Configuration["Jwt:Issuer"] + "/swagger/oauth2-redirect.html"); // adres api server
                    c.OAuthUsePkce(); // mechanizm ma za zadanie rozpoznawac czy kod autoryzacyjny, tokeny byly uzywane przez inne aplikacje dodatkowe przed atakami na strone
                });
            }

            app.UseHttpsRedirection();
            app.UseMiddleware<ExceptionMiddleware>();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
