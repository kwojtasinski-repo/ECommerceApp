using ECommerceApp.Application.External.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System;

namespace ECommerceApp.Application.External
{
    internal static class Extensions
    {
        public static IServiceCollection AddNbpClient(this IServiceCollection services)
        {
            // http client
            services.AddHttpClient("NBPClient", options =>
            {
                options.Timeout = new TimeSpan(0, 0, 15);
                options.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            }).ConfigurePrimaryHttpMessageHandler(sp => new HttpClientHandler());
            services.AddScoped<INBPClient, NBPClient>();
            return services;
        }
    }
}
