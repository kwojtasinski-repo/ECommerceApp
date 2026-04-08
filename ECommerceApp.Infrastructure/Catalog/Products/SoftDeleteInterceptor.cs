using ECommerceApp.Domain.Catalog.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Catalog.Products
{
    internal sealed class SoftDeleteInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            ApplySoftDelete(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ApplySoftDelete(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void ApplySoftDelete(DbContext context)
        {
            if (context is null)
            {
                return;
            }

            foreach (var entry in context.ChangeTracker.Entries<Image>()
                .Where(e => e.State == EntityState.Deleted)
                .ToList())
            {
                entry.State = EntityState.Modified;
                entry.Property(nameof(Image.IsDeleted)).CurrentValue = true;
            }
        }
    }
}
