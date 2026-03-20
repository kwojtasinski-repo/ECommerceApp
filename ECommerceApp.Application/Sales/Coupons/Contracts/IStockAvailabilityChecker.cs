using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Contracts
{
    public interface IStockAvailabilityChecker
    {
        Task<bool> IsInStockAsync(int productId, int quantity, CancellationToken ct = default);
    }
}
