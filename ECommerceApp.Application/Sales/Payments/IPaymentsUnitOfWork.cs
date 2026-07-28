using ECommerceApp.Application.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments
{
    /// <summary>
    /// Payments-BC-specific transaction starter.
    /// </summary>
    public interface IPaymentsUnitOfWork
    {
        Task<IOutboxTransaction> BeginTransactionAsync(CancellationToken ct = default);
    }
}
