using AutoMapper.Internal;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.External.Client;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Middlewares;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.Services.Orders;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Reflection;

namespace ECommerceApp.Application
{
    public static class DependencyInjection 
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddTransient<IOrderService, OrderService>();
            services.AddAutoMapper(cfg => cfg.Internal().MethodMappingEnabled = false, Assembly.GetExecutingAssembly());
            services.AddTransient<IFileStore, FileStore>();
            services.AddTransient<IFileWrapper, FileWrapper>();
            services.AddTransient<IDirectoryWrapper, DirectoryWrapper>();
            services.AddTransient<IOrderItemService, OrderItemService>();

            services.AddSingleton<IErrorMapToResponse, ErrorMapToResponse>();
            services.AddTransient<ExceptionMiddleware>();

            // http client
            services.AddHttpClient("NBPClient", options =>
            {
                options.Timeout = new TimeSpan(0, 0, 15);
                options.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            }).ConfigurePrimaryHttpMessageHandler(sp => new HttpClientHandler());
            services.AddScoped<INBPClient, NBPClient>();


            services.AddServices();

            return services;
        }
    }
}
