using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace ECommerceApp.Infrastructure.Database
{
    internal static class Extensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<Context>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
                       .LogTo(Console.WriteLine,
                                                                // if want more info change to debug
                            (eventId, logLevel) => logLevel >= LogLevel.Information
                                                   || eventId == RelationalEventId.ConnectionOpened
                                                   || eventId == RelationalEventId.ConnectionClosed)
                       .EnableSensitiveDataLogging()
                );

            services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                     .AddRoles<IdentityRole>()
                     .AddEntityFrameworkStores<Context>();

            services.AddHostedService<DbInitializer>();
            services.AddScoped<IDatabaseInitializer, DatabaseInitalizer>();

            return services;
        }
    }
}
