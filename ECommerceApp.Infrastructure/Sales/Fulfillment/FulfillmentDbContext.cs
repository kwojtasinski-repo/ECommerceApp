using ECommerceApp.Domain.Sales.Fulfillment;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Fulfillment
{
    internal sealed class FulfillmentDbContext : DbContext
    {
        public DbSet<Refund> Refunds => Set<Refund>();
        public DbSet<Shipment> Shipments => Set<Shipment>();

        public FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema(FulfillmentConstants.SchemaName);
            modelBuilder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Sales.Fulfillment.Configurations"));
            modelBuilder.UseUtcDateTimes();
        }
    }
}
