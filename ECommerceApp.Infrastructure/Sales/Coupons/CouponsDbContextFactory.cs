using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Sales.Coupons
{
    internal sealed class CouponsDbContextFactory : IDesignTimeDbContextFactory<CouponsDbContext>
    {
        public CouponsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CouponsDbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new CouponsDbContext(optionsBuilder.Options);
        }
    }
}
