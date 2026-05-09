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
            services.AddHostedService<DbInitializer>();
            services.AddScoped<IDatabaseInitializer, DatabaseInitalizer>();
            return services;
        }
    }
}
