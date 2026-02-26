using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal static class Extensions
    {
        public static IServiceCollection AddMessagingInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var messagingOptions = new MessagingOptions();
            configuration.GetSection(nameof(MessagingOptions)).Bind(messagingOptions);
            services.AddSingleton(messagingOptions);

            services.AddSingleton<IMessageChannel, MessageChannel>();
            services.AddSingleton<IAsyncMessageDispatcher, AsyncMessageDispatcher>();
            services.AddScoped<IModuleClient, ModuleClient>();
            services.AddScoped<IMessageBroker, InMemoryMessageBroker>();

            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BackgroundMessageDispatcher>());

            return services;
        }
    }
}
