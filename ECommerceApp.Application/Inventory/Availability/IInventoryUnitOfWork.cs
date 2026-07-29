using ECommerceApp.Application.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability
{
    /// <summary>
    /// Availability-BC-specific transaction starter.
    /// </summary>
    public interface IInventoryUnitOfWork
    {
        Task<IOutboxTransaction> BeginTransactionAsync(CancellationToken ct = default);
    }
}
