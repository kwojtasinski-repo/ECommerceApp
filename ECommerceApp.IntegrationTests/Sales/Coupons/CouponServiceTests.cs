using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Coupons
{
    public class CouponServiceTests : BcBaseTest<ICouponService>
    {
        // ── ApplyCouponAsync ─────────────────────────────────────────────

        [Fact]
        public async Task ApplyCouponAsync_NonExistentOrder_ShouldReturnOrderNotFound()
        {
            var result = await _service.ApplyCouponAsync("PROMO-123", new CouponEvaluationContext(int.MaxValue, "user-1", 0m, new List<CouponEvaluationItem>()));

            result.ShouldBe(CouponApplyResult.OrderNotFound);
        }

        // ── RemoveCouponAsync ────────────────────────────────────────────

        [Fact]
        public async Task RemoveCouponAsync_NoCouponApplied_ShouldReturnNoCouponApplied()
        {
            var result = await _service.RemoveCouponAsync(orderId: int.MaxValue);

            result.ShouldBe(CouponRemoveResult.NoCouponApplied);
        }
    }
}
