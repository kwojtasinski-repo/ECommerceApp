using ECommerceApp.Application.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ECommerceApp.Infrastructure.Messaging.Repositories;
using ECommerceApp.Infrastructure.Database;

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

            // Outbox persistence (Phase 1 of the outbox rollout)
            services.AddDbContext<MessagingDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IMessagingDbContext>(sp => sp.GetRequiredService<MessagingDbContext>())
                .AddScoped<IOutboxRepository, OutboxRepository>();

            services.AddScoped<IDbContextMigrator, DbContextMigrator<MessagingDbContext>>();

            services.AddScoped<OutboxDispatcher>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxPollerService>());
            services.AddScoped<IOutboxWriter, OutboxWriter>();

            return services;
        }
    }
}
