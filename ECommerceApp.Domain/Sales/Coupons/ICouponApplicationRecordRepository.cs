using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public interface ICouponApplicationRecordRepository
    {
        Task AddAsync(CouponApplicationRecord record, CancellationToken ct = default);
        Task<CouponApplicationRecord?> FindByCouponUsedIdAsync(int couponUsedId, CancellationToken ct = default);
        Task<IReadOnlyList<CouponApplicationRecord>> FindByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task UpdateAsync(CouponApplicationRecord record, CancellationToken ct = default);
    }
}
