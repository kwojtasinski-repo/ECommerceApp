using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Application.Sagas;
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

            return services;
        }
    }
}