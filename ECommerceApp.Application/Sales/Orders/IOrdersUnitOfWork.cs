using ECommerceApp.Application.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders
{
    /// <summary>
    /// Orders-BC-specific transaction starter.
    /// </summary>
    public interface IOrdersUnitOfWork
    {
        Task<IOutboxTransaction> BeginTransactionAsync(CancellationToken ct = default);
    }
}
