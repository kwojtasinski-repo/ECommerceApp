using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Catalog.Products
{
    internal sealed class ProductDbContextFactory : IDesignTimeDbContextFactory<ProductDbContext>
    {
        public ProductDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ProductDbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new ProductDbContext(optionsBuilder.Options);
        }
    }
}
