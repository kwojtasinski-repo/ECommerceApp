using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Middlewares
{
    internal static class Extensions
    {
        public static IServiceCollection AddErrorHandling(this IServiceCollection services) 
        {
            return services.AddSingleton<IErrorMapToResponse, ErrorMapToResponse>()
                           .AddTransient<ExceptionMiddleware>();
        }
    }
}
