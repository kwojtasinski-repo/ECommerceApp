using ECommerceApp.Domain.Sales.Orders;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Orders
{
    internal sealed class OrdersDbContext : DbContext
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema(OrdersConstants.SchemaName);
            modelBuilder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Sales.Orders.Configurations"));
        }
    }
}
