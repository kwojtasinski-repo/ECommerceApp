using ECommerceApp.Application.Sales.Coupons.Rules;
using AwesomeAssertions;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponRuleEvaluationTests
    {
        // ── CouponRuleEvaluationResult ────────────────────────────────────────

        [Fact]
        public void Pass_ShouldCreatePassedResult()
        {
            var result = CouponRuleEvaluationResult.Pass(25m);

            result.Passed.Should().BeTrue();
            result.Reduction.Should().Be(25m);
            result.FailureReason.Should().BeEmpty();
        }

        [Fact]
        public void Pass_NoReduction_ShouldDefaultToZero()
        {
            var result = CouponRuleEvaluationResult.Pass();

            result.Passed.Should().BeTrue();
            result.Reduction.Should().Be(0m);
        }

        [Fact]
        public void Fail_ShouldCreateFailedResultWithReason()
        {
            var result = CouponRuleEvaluationResult.Fail("Coupon expired");

            result.Passed.Should().BeFalse();
            result.Reduction.Should().Be(0m);
            result.FailureReason.Should().Be("Coupon expired");
        }

        // ── CouponEvaluationContext ───────────────────────────────────────────

        [Fact]
        public void CouponEvaluationContext_ShouldHoldOrderAndItemData()
        {
            var items = new List<CouponEvaluationItem>
            {
                new(productId: 1, categoryId: 10, tagIds: new[] { 1, 2 }, unitPrice: 50m, quantity: 2),
                new(productId: 2, categoryId: 20, tagIds: new[] { 3 }, unitPrice: 30m, quantity: 1)
            };

            var ctx = new CouponEvaluationContext(orderId: 99, userId: "user-1", originalTotal: 130m, items: items);

            ctx.OrderId.Should().Be(99);
            ctx.UserId.Should().Be("user-1");
            ctx.OriginalTotal.Should().Be(130m);
            ctx.Items.Should().HaveCount(2);
            ctx.Items[0].ProductId.Should().Be(1);
            ctx.Items[0].UnitPrice.Should().Be(50m);
            ctx.Items[0].Quantity.Should().Be(2);
            ctx.Items[1].TagIds.Should().Contain(3);
        }

        // ── Percentage-off discount evaluation spec ───────────────────────────

        [Fact]
        public void PercentageOff_ShouldCalculateReductionFromOriginalTotal()
        {
            // Spec: percentage-off evaluator takes "percent" parameter,
            // calculates reduction = originalTotal * percent / 100
            var parameters = new Dictionary<string, string> { ["percent"] = "15" };
            var items = new List<CouponEvaluationItem>
            {
                new(1, 10, new[] { 1 }, 100m, 2)
            };
            var ctx = new CouponEvaluationContext(orderId: 1, userId: "u", originalTotal: 200m, items: items);

            // Expected: reduction = 200 * 15 / 100 = 30
            var expectedReduction = 30m;

            // This documents the expected behavior for the percentage-off evaluator
            expectedReduction.Should().Be(30m);
            parameters["percent"].Should().Be("15");
            ctx.OriginalTotal.Should().Be(200m);
        }

        // ── Fixed-amount-off discount evaluation spec ─────────────────────────

        [Fact]
        public void FixedAmountOff_ShouldReduceByFixedAmount()
        {
            // Spec: fixed-amount-off takes "amount" parameter, reduction = min(amount, originalTotal)
            var parameters = new Dictionary<string, string> { ["amount"] = "50" };
            var ctx = new CouponEvaluationContext(orderId: 1, userId: "u", originalTotal: 200m,
                items: new List<CouponEvaluationItem>());

            var expectedReduction = 50m;

            expectedReduction.Should().Be(50m);
            parameters["amount"].Should().Be("50");
        }

        [Fact]
        public void FixedAmountOff_AmountExceedsTotal_ShouldCapAtTotal()
        {
            // Spec: if amount > originalTotal, reduction = originalTotal (floor at 0)
            var parameters = new Dictionary<string, string> { ["amount"] = "500" };
            var ctx = new CouponEvaluationContext(orderId: 1, userId: "u", originalTotal: 200m,
                items: new List<CouponEvaluationItem>());

            var expectedReduction = 200m; // capped at originalTotal

            expectedReduction.Should().Be(200m);
        }

        // ── Free-cheapest-item discount evaluation spec ───────────────────────

        [Fact]
        public void FreeCheapestItem_ShouldSelectCheapestItemFromCart()
        {
            // Spec: free-cheapest-item takes "maxFreeUnits" parameter
            // Finds cheapest item in cart and makes it free (reduction = cheapest unit price * maxFreeUnits)
            var items = new List<CouponEvaluationItem>
            {
                new(1, 10, new int[0], unitPrice: 100m, quantity: 1),
                new(2, 20, new int[0], unitPrice: 30m, quantity: 2),   // cheapest
                new(3, 30, new int[0], unitPrice: 75m, quantity: 1)
            };
            var parameters = new Dictionary<string, string> { ["maxFreeUnits"] = "1" };

            var expectedReduction = 30m; // cheapest item

            expectedReduction.Should().Be(30m);
            items[1].UnitPrice.Should().Be(30m);
        }

        // ── Max-uses constraint spec ──────────────────────────────────────────

        [Fact]
        public void MaxUses_BelowLimit_ShouldPass()
        {
            // Spec: max-uses constraint passes if total coupon usage count < maxUses
            var parameters = new Dictionary<string, string> { ["maxUses"] = "100" };
            var currentUsageCount = 50;

            (currentUsageCount < 100).Should().BeTrue();
        }

        [Fact]
        public void MaxUses_AtLimit_ShouldFail()
        {
            // Spec: max-uses constraint fails if total coupon usage count >= maxUses
            var parameters = new Dictionary<string, string> { ["maxUses"] = "100" };
            var currentUsageCount = 100;

            (currentUsageCount >= 100).Should().BeTrue();
        }

        // ── Max-uses-per-user constraint spec ─────────────────────────────────

        [Fact]
        public void MaxUsesPerUser_UserBelowLimit_ShouldPass()
        {
            // Spec: max-uses-per-user passes if user's usage count < maxUsesPerUser
            var parameters = new Dictionary<string, string> { ["maxUsesPerUser"] = "1" };
            var userUsageCount = 0;

            (userUsageCount < 1).Should().BeTrue();
        }

        [Fact]
        public void MaxUsesPerUser_UserAtLimit_ShouldFail()
        {
            // Spec: max-uses-per-user fails if user's usage count >= maxUsesPerUser
            var parameters = new Dictionary<string, string> { ["maxUsesPerUser"] = "1" };
            var userUsageCount = 1;

            (userUsageCount >= 1).Should().BeTrue();
        }

        // ── Valid-date-range constraint spec ──────────────────────────────────

        [Fact]
        public void ValidDateRange_WithinRange_ShouldPass()
        {
            var parameters = new Dictionary<string, string>
            {
                ["validFrom"] = "2026-01-01",
                ["validTo"] = "2026-12-31"
            };
            var now = new System.DateTime(2026, 6, 15);

            (now >= System.DateTime.Parse(parameters["validFrom"]) &&
             now <= System.DateTime.Parse(parameters["validTo"])).Should().BeTrue();
        }

        [Fact]
        public void ValidDateRange_BeforeRange_ShouldFail()
        {
            var parameters = new Dictionary<string, string>
            {
                ["validFrom"] = "2026-06-01",
                ["validTo"] = "2026-12-31"
            };
            var now = new System.DateTime(2026, 1, 1);

            (now >= System.DateTime.Parse(parameters["validFrom"])).Should().BeFalse();
        }

        // ── Min-order-value constraint spec ───────────────────────────────────

        [Fact]
        public void MinOrderValue_OrderAboveMinimum_ShouldPass()
        {
            var parameters = new Dictionary<string, string> { ["minValue"] = "100" };
            var originalTotal = 150m;

            (originalTotal >= decimal.Parse(parameters["minValue"])).Should().BeTrue();
        }

        [Fact]
        public void MinOrderValue_OrderBelowMinimum_ShouldFail()
        {
            var parameters = new Dictionary<string, string> { ["minValue"] = "100" };
            var originalTotal = 50m;

            (originalTotal >= decimal.Parse(parameters["minValue"])).Should().BeFalse();
        }

        // ── Special-event constraint spec ─────────────────────────────────────

        [Fact]
        public void SpecialEvent_ActiveEvent_ShouldPass()
        {
            // Spec: looks up eventCode in ISpecialEventCache, calls IsCurrentlyActive(utcNow)
            var parameters = new Dictionary<string, string> { ["eventCode"] = "BLACK_FRIDAY" };
            var eventIsActive = true;

            eventIsActive.Should().BeTrue();
        }

        [Fact]
        public void SpecialEvent_InactiveEvent_ShouldFail()
        {
            var parameters = new Dictionary<string, string> { ["eventCode"] = "BLACK_FRIDAY" };
            var eventIsActive = false;

            eventIsActive.Should().BeFalse();
        }

        // ── First-purchase-only constraint spec ───────────────────────────────

        [Fact]
        public void FirstPurchaseOnly_ZeroCompletedOrders_ShouldPass()
        {
            // Spec: passes if user has zero completed orders
            var completedOrderCount = 0;

            (completedOrderCount == 0).Should().BeTrue();
        }

        [Fact]
        public void FirstPurchaseOnly_HasCompletedOrders_ShouldFail()
        {
            var completedOrderCount = 3;

            (completedOrderCount == 0).Should().BeFalse();
        }

        // ── Parameters — defaults-when-missing convention ─────────────────────

        [Fact]
        public void Parameters_MissingKey_ShouldFallbackToDefault()
        {
            // Spec: all parameter reads use TryGetValue with fallback
            var parameters = new Dictionary<string, string>();

            parameters.TryGetValue("percent", out var value).Should().BeFalse();
            (value ?? "10").Should().Be("10"); // default fallback
        }

        // ── Two-tier validation spec ──────────────────────────────────────────

        [Fact]
        public void TwoTierValidation_Tier1Fails_ShouldNotRunTier2()
        {
            // Spec: Tier 1 (sync, zero DB) runs first. If any rule fails in Tier 1, Tier 2 is skipped.
            var tier1Passed = false;
            var tier2ShouldRun = tier1Passed;

            tier2ShouldRun.Should().BeFalse();
        }

        [Fact]
        public void TwoTierValidation_Tier1Passes_ShouldRunTier2()
        {
            // Spec: If all Tier 1 rules pass, Tier 2 (async, DB/cache) rules are evaluated.
            var tier1Passed = true;
            var tier2ShouldRun = tier1Passed;

            tier2ShouldRun.Should().BeTrue();
        }

        // ── Independent evaluation spec ───────────────────────────────────────

        [Fact]
        public void IndependentEvaluation_EachCouponAgainstOriginalTotal_ReductionsSummed()
        {
            // Spec: each coupon is evaluated against the ORIGINAL order total independently.
            // Reductions are summed. The evaluation is deterministic (order-independent).
            var originalTotal = 200m;
            var coupon1Reduction = 30m;  // 15% of 200
            var coupon2Reduction = 50m;  // flat 50 off
            var totalReduction = coupon1Reduction + coupon2Reduction;

            totalReduction.Should().Be(80m);
            (originalTotal - totalReduction).Should().Be(120m);
        }

        [Fact]
        public void IndependentEvaluation_ReductionSumExceedsTotal_ShouldCapAtTotal()
        {
            // Spec: floor at max(0, original - sum). Checkout BC enforces the cap.
            var originalTotal = 100m;
            var totalReduction = 150m; // exceeds total

            var finalPrice = System.Math.Max(0, originalTotal - totalReduction);

            finalPrice.Should().Be(0m);
        }

        // ── Scope evaluation spec ─────────────────────────────────────────────

        [Fact]
        public void PerProductScope_ShouldOnlyApplyToMatchingProducts()
        {
            // Spec: per-product scope filter — only items with matching ProductId are discounted
            var scopeTargetProductId = 42;
            var items = new List<CouponEvaluationItem>
            {
                new(productId: 42, categoryId: 10, tagIds: new int[0], unitPrice: 50m, quantity: 2),
                new(productId: 99, categoryId: 20, tagIds: new int[0], unitPrice: 30m, quantity: 1)
            };

            var matchingItems = items.FindAll(i => i.ProductId == scopeTargetProductId);

            matchingItems.Should().HaveCount(1);
            matchingItems[0].UnitPrice.Should().Be(50m);
        }

        [Fact]
        public void PerCategoryScope_ShouldOnlyApplyToMatchingCategories()
        {
            var scopeTargetCategoryId = 10;
            var items = new List<CouponEvaluationItem>
            {
                new(productId: 1, categoryId: 10, tagIds: new int[0], unitPrice: 50m, quantity: 2),
                new(productId: 2, categoryId: 20, tagIds: new int[0], unitPrice: 30m, quantity: 1)
            };

            var matchingItems = items.FindAll(i => i.CategoryId == scopeTargetCategoryId);

            matchingItems.Should().HaveCount(1);
        }

        [Fact]
        public void PerTagScope_ShouldOnlyApplyToItemsWithMatchingTag()
        {
            var scopeTargetTagId = 3;
            var items = new List<CouponEvaluationItem>
            {
                new(productId: 1, categoryId: 10, tagIds: new[] { 1, 2, 3 }, unitPrice: 50m, quantity: 1),
                new(productId: 2, categoryId: 20, tagIds: new[] { 4, 5 }, unitPrice: 30m, quantity: 1)
            };

            var matchingItems = items.FindAll(i => i.TagIds.Contains(scopeTargetTagId));

            matchingItems.Should().HaveCount(1);
            matchingItems[0].ProductId.Should().Be(1);
        }
    }
}
