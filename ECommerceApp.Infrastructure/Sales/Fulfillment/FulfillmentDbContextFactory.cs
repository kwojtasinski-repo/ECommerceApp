using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Sales.Fulfillment
{
    internal sealed class FulfillmentDbContextFactory : IDesignTimeDbContextFactory<FulfillmentDbContext>
    {
        public FulfillmentDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FulfillmentDbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new FulfillmentDbContext(optionsBuilder.Options);
        }
    }
}
