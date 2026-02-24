using AutoMapper.Internal;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.External;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Middlewares;
using ECommerceApp.Application.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
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
            services.AddIamServices();
            services.AddUserProfileServices();
            services.AddCatalogServices();
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
            services.AddFluentValidationAutoValidation();
            return services;
        }
    }
}
