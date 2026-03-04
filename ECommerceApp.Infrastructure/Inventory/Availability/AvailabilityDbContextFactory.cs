using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Inventory.Availability
{
    internal sealed class AvailabilityDbContextFactory : IDesignTimeDbContextFactory<AvailabilityDbContext>
    {
        public AvailabilityDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AvailabilityDbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new AvailabilityDbContext(optionsBuilder.Options);
        }
    }
}
