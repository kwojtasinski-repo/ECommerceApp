using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Contracts
{
    public interface ICompletedOrderCounter
    {
        Task<int> CountByUserAsync(string userId, CancellationToken ct = default);
    }
}
