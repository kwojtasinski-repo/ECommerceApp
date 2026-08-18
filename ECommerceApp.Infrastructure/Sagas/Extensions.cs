using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sagas;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Fulfillment.Sagas;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Application.Presale.Checkout.Sagas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Sagas
{
    internal static class Extensions
    {
        public static IServiceCollection AddSagaInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<SagasDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IDbContextMigrator, DbContextMigrator<SagasDbContext>>();
            services.AddScoped<ISagaPayloadSerializer, SagaPayloadSerializer>();
            services.AddScoped<ISagaUnitOfWork, SagaUnitOfWork>();
            services.AddScoped<ISagaRepository, SagaRepository>();
            services.AddScoped<ISagaDefinition, RefundSagaDefinition>();
            services.AddScoped<IMessageHandler<RefundApproved>, SagaTransitionHandler<RefundApproved>>();
            services.AddScoped<IMessageHandler<RefundStockReturned>, SagaTransitionHandler<RefundStockReturned>>();
            services.AddScoped<IMessageHandler<RefundCustomerNotified>, SagaTransitionHandler<RefundCustomerNotified>>();
            services.AddScoped<ISagaDefinition, CartRecoverySagaDefinition>();
            services.AddScoped<IMessageHandler<CheckoutReservationRevertRequested>, SagaTransitionHandler<CheckoutReservationRevertRequested>>();

            return services;
        }
    }
}