using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Contracts
{
    public interface IOrderExistenceChecker
    {
        Task<bool> ExistsAsync(int orderId, CancellationToken ct = default);
    }
}
