using ECommerceApp.Domain.Sales.Coupons;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Contracts
{
    public interface ISpecialEventCache
    {
        Task<SpecialEvent> GetByCodeAsync(string eventCode, CancellationToken ct = default);
        void Invalidate();
    }
}
