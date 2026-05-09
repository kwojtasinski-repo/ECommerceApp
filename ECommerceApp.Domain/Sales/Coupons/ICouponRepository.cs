using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public interface ICouponRepository
    {
        Task<Coupon> GetByCodeAsync(string code, CancellationToken ct = default);
        Task<Coupon> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<Coupon>> GetAllAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default);
        Task<int> CountAsync(string searchString, CancellationToken ct = default);
        Task UpdateAsync(Coupon coupon, CancellationToken ct = default);
        Task AddAsync(Coupon coupon, CancellationToken ct = default);
        Task DeleteAsync(Coupon coupon, CancellationToken ct = default);
    }
}
