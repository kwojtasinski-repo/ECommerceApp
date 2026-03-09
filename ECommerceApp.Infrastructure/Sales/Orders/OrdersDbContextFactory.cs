using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Sales.Orders
{
    internal sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
    {
        public OrdersDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OrdersDbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new OrdersDbContext(optionsBuilder.Options);
        }
    }
}
