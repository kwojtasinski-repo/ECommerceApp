using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ECommerceApp.Infrastructure.Catalog.Products
{
    internal sealed class CatalogDbContext : DbContext, ICatalogDbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Image> Images { get; set; }
        public DbSet<ProductTag> ProductTags { get; set; }

        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema(CatalogConstants.SchemaName);
            builder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Catalog.Products.Configurations"));
            builder.UseUtcDateTimes();
        }
    }
}
