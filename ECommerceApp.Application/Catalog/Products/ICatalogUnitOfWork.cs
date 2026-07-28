using ECommerceApp.Application.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products
{
    /// <summary>
    /// Catalog-BC-specific transaction starter.
    /// </summary>
    public interface ICatalogUnitOfWork
    {
        Task<IOutboxTransaction> BeginTransactionAsync(CancellationToken ct = default);
    }
}
