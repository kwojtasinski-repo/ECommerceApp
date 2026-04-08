using ECommerceApp.Domain.Catalog.Products;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Catalog.Products
{
    internal interface ICatalogDbContext
    {
        DbSet<Product> Products { get; }
        DbSet<Category> Categories { get; }
        DbSet<Tag> Tags { get; }
        DbSet<Image> Images { get; }
        DbSet<ProductTag> ProductTags { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
