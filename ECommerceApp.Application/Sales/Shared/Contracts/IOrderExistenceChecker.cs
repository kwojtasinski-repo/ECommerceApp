using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Shared.Contracts
{
    public interface IOrderExistenceChecker
    {
        Task<bool> ExistsAsync(int orderId, CancellationToken ct = default);
    }
}
