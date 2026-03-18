using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Coupons.Contracts
{
    public sealed class RuntimeCoupon
    {
        public string Code { get; set; }
        public string Source { get; set; }
        public int DiscountPercent { get; set; }
        public string Scope { get; set; }
    }

    public interface IRuntimeCouponSource
    {
        Task<RuntimeCoupon> SuggestCouponAsync(string userId, object context, CancellationToken ct = default);
    }

    public sealed class NullRuntimeCouponSource : IRuntimeCouponSource
    {
        public Task<RuntimeCoupon> SuggestCouponAsync(string userId, object context, CancellationToken ct = default)
            => Task.FromResult<RuntimeCoupon>(null);
    }
}
