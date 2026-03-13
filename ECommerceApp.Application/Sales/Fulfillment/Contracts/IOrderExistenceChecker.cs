using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Fulfillment.Contracts
{
    public interface IOrderExistenceChecker
    {
        Task<bool> ExistsAsync(int orderId, CancellationToken ct = default);
    }
}
