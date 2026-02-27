using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Catalog.Products
{
    internal sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
    {
        public CatalogDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new CatalogDbContext(optionsBuilder.Options);
        }
    }
}
