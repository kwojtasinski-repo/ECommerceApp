using ECommerceApp.Application.Presale.Checkout;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Presale.Checkout.Adapters;
using ECommerceApp.Infrastructure.Presale.Checkout.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Presale.Checkout
{
    internal static class Extensions
    {
        public static IServiceCollection AddPresaleInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<PresaleOptions>(configuration.GetSection(PresaleOptions.SectionName));

            services.AddDbContext<PresaleDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IPresaleDbContext>(sp => sp.GetRequiredService<PresaleDbContext>());

            services.AddScoped<IDbContextMigrator, DbContextMigrator<PresaleDbContext>>();

            return services
                .AddScoped<ICatalogClient, CatalogClientAdapter>()
                .AddScoped<IAccountProfileClient, AccountProfileClientAdapter>()
                .AddScoped<IOrderClient, OrderClientAdapter>()
                .AddScoped<ICartLineRepository, CartLineRepository>()
                .AddScoped<ISoftReservationRepository, SoftReservationRepository>()
                .AddScoped<IStockSnapshotRepository, StockSnapshotRepository>();
        }
    }
}
