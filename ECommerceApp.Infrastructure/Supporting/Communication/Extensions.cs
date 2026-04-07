using ECommerceApp.Application.Supporting.Communication.Emails;
using ECommerceApp.Application.Supporting.Communication.Services;
using ECommerceApp.Infrastructure.Supporting.Communication.Options;
using ECommerceApp.Infrastructure.Supporting.Communication.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Supporting.Communication
{
    internal static class Extensions
    {
        public static IServiceCollection AddCommunicationInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSignalR();
            services.AddScoped<INotificationService, SignalRNotificationService>();
            services.Configure<SmtpEmailOptions>(configuration.GetSection("Smtp"));
            services.AddScoped<IEmailService, SmtpEmailService>();
            return services;
        }
    }
}
