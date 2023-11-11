using AutoMapper.Internal;
using ECommerceApp.Application.External;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Middlewares;
using ECommerceApp.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ECommerceApp.Application
{
    public static class DependencyInjection 
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddAutoMapper(cfg => cfg.Internal().MethodMappingEnabled = false, Assembly.GetExecutingAssembly());
            services.AddFilesStore();
            services.AddErrorHandling();
            services.AddNbpClient();
            services.AddServices();
            return services;
        }
    }
}
