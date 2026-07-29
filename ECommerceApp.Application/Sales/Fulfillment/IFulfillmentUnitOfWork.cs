using ECommerceApp.Application.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Fulfillment
{
    /// <summary>
    /// Fulfillment-BC-specific transaction starter.
    /// </summary>
    public interface IFulfillmentUnitOfWork
    {
        Task<IOutboxTransaction> BeginTransactionAsync(CancellationToken ct = default);
    }
}
