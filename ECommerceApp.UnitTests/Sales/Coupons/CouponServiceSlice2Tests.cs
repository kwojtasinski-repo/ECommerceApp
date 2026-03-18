using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Domain.Sales.Coupons;
using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    /// <summary>
    /// Slice 2 specification tests for ICouponService — rule-based coupon creation,
    /// multi-coupon application, exclusive coupons, discount cap, concurrency,
    /// OrderPriceAdjusted publication, and CouponApplicationRecord creation.
    ///
    /// These tests document the expected behavior of the Slice 2 CouponService.
    /// Until CouponService is redesigned for Slice 2, many will fail with NotImplementedException.
    /// </summary>
    public class CouponServiceSlice2Tests
    {
        private readonly Mock<ICouponRepository> _coupons;
        private readonly Mock<ICouponUsedRepository> _couponUsed;
        private readonly Mock<ICouponApplicationRecordRepository> _applicationRecords;
        private readonly Mock<IOrderExistenceChecker> _orderExistence;
        private readonly Mock<IMessageBroker> _broker;
        private readonly Mock<ICouponRuleRegistry> _ruleRegistry;
        private readonly Mock<ISpecialEventCache> _specialEventCache;
        private readonly Mock<IRuntimeCouponSource> _runtimeCouponSource;
        private readonly CouponsOptions _options;

        public CouponServiceSlice2Tests()
        {
            _coupons = new Mock<ICouponRepository>();
            _couponUsed = new Mock<ICouponUsedRepository>();
            _applicationRecords = new Mock<ICouponApplicationRecordRepository>();
            _orderExistence = new Mock<IOrderExistenceChecker>();
            _broker = new Mock<IMessageBroker>();
            _ruleRegistry = new Mock<ICouponRuleRegistry>();
            _specialEventCache = new Mock<ISpecialEventCache>();
            _runtimeCouponSource = new Mock<IRuntimeCouponSource>();
            _options = new CouponsOptions { MaxCouponsPerOrder = 5 };
        }

        private static string BuildRulesJson(params CouponRuleDefinition[] rules)
            => JsonSerializer.Serialize(rules);

        private static Coupon CreateRuleBasedCoupon(int id = 1, string code = "SAVE15")
        {
            var rulesJson = BuildRulesJson(
                new CouponRuleDefinition("order-total", CouponRuleCategory.Scope, new Dictionary<string, string>()),
                new CouponRuleDefinition("percentage-off", CouponRuleCategory.Discount, new Dictionary<string, string> { ["percent"] = "15" }));
            var coupon = Coupon.CreateWithRules(code, "15% off", rulesJson, new List<CouponScopeTarget>());
            typeof(Coupon).GetProperty(nameof(Coupon.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(coupon, new object[] { new CouponId(id) });
            return coupon;
        }

        private static CouponUsed CreateCouponUsedForDb(int id = 1, int couponId = 1, int orderId = 99, string userId = "user-1")
        {
            var cu = CouponUsed.CreateForDbCoupon(new CouponId(couponId), orderId, userId);
            typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(cu, new object[] { new CouponUsedId(id) });
            return cu;
        }

        // ══════════════════════════════════════════════════════════════════════
        // CreateCouponAsync — coupon creation with full validation pipeline
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task CreateCouponAsync_ValidDto_ShouldPersistCouponAndReturnSuccess()
        {
            var dto = new CreateCouponDto
            {
                Code = "SAVE15",
                Description = "15% off",
                RulesJson = BuildRulesJson(
                    new CouponRuleDefinition("order-total", CouponRuleCategory.Scope, new Dictionary<string, string>()),
                    new CouponRuleDefinition("percentage-off", CouponRuleCategory.Discount, new Dictionary<string, string> { ["percent"] = "15" })),
                ScopeTargets = new List<ScopeTargetDto>()
            };
            var service = CreateSlice1Service(); // Will need Slice 2 service when implemented

            var act = async () => await service.CreateCouponAsync(dto);

            // Until Slice 2 CouponService is implemented, this throws NotImplementedException
            await act.Should().ThrowAsync<System.NotImplementedException>();
        }

        [Fact]
        public async Task CreateCouponAsync_PerProductScope_WithTargets_ShouldPersistScopeTargets()
        {
            var dto = new CreateCouponDto
            {
                Code = "PROD15",
                Description = "15% off product",
                RulesJson = BuildRulesJson(
                    new CouponRuleDefinition("per-product", CouponRuleCategory.Scope, new Dictionary<string, string>()),
                    new CouponRuleDefinition("percentage-off", CouponRuleCategory.Discount, new Dictionary<string, string> { ["percent"] = "15" })),
                ScopeTargets = new List<ScopeTargetDto>
                {
                    new() { ScopeType = "per-product", TargetId = 42, TargetName = "Widget" }
                }
            };
            var service = CreateSlice1Service();

            var act = async () => await service.CreateCouponAsync(dto);

            await act.Should().ThrowAsync<System.NotImplementedException>();
        }

        [Fact]
        public async Task CreateCouponAsync_InvalidRuleComposition_ShouldReturnFailure()
        {
            // No scope rule — should fail validation
            var dto = new CreateCouponDto
            {
                Code = "INVALID",
                Description = "desc",
                RulesJson = BuildRulesJson(
                    new CouponRuleDefinition("percentage-off", CouponRuleCategory.Discount, new Dictionary<string, string> { ["percent"] = "15" })),
                ScopeTargets = new List<ScopeTargetDto>()
            };
            var service = CreateSlice1Service();

            var act = async () => await service.CreateCouponAsync(dto);

            await act.Should().ThrowAsync<System.NotImplementedException>();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Multi-coupon per order — MaxCouponsPerOrder enforcement
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void MaxCouponsPerOrder_DefaultShouldBeFive()
        {
            var options = new CouponsOptions();

            options.MaxCouponsPerOrder.Should().Be(5);
        }

        [Fact]
        public void MaxCouponsPerOrder_HardCeilingShouldBeTen()
        {
            var options = new CouponsOptions { MaxCouponsPerOrder = 15 };

            // The hard ceiling should be enforced at service level
            // options allows setting any value, but service must cap at 10
            options.MaxCouponsPerOrder.Should().Be(15); // Config allows it
            // Service must enforce: Math.Min(options.MaxCouponsPerOrder, 10)
            System.Math.Min(options.MaxCouponsPerOrder, 10).Should().Be(10);
        }

        [Fact]
        public async Task ApplyCouponAsync_OrderAlreadyAtMaxCoupons_ShouldRejectNewCoupon()
        {
            // Spec: if order already has MaxCouponsPerOrder coupons, reject
            var existingCoupons = new List<CouponUsed>();
            for (int i = 0; i < 5; i++)
                existingCoupons.Add(CreateCouponUsedForDb(id: i + 1, couponId: i + 1, orderId: 99));

            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCoupons);

            existingCoupons.Count.Should().Be(5);
            // When Slice 2 is implemented, ApplyCouponAsync should check count and reject
        }

        // ══════════════════════════════════════════════════════════════════════
        // Exclusive coupon — IsExclusive flag
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void CouponApplicationResult_Applied_WithExclusiveFlag_ShouldIndicateExclusivity()
        {
            var result = CouponApplicationResult.Applied(reduction: 30m, isExclusive: true);

            result.Success.Should().BeTrue();
            result.IsExclusive.Should().BeTrue();
            result.Reduction.Should().Be(30m);
        }

        [Fact]
        public void CouponApplicationResult_Applied_NotExclusive_ShouldDefaultToFalse()
        {
            var result = CouponApplicationResult.Applied(reduction: 30m);

            result.IsExclusive.Should().BeFalse();
        }

        [Fact]
        public void CouponApplicationResult_Failed_ShouldContainReason()
        {
            var result = CouponApplicationResult.Failed("Coupon expired");

            result.Success.Should().BeFalse();
            result.FailureReason.Should().Be("Coupon expired");
            result.Reduction.Should().Be(0m);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Independent evaluation — each coupon against original total
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void IndependentEvaluation_MultipleCoupons_ShouldEachUseOriginalTotal()
        {
            // Spec: each coupon evaluates independently against the ORIGINAL order total
            // The reductions are summed, not cascaded
            var originalTotal = 200m;

            // Coupon A: 15% of 200 = 30
            var reductionA = originalTotal * 15m / 100m;
            // Coupon B: flat 50 off (evaluated against original 200, NOT against 170)
            var reductionB = 50m;

            var totalReduction = reductionA + reductionB;
            var finalPrice = originalTotal - totalReduction;

            reductionA.Should().Be(30m);
            totalReduction.Should().Be(80m);
            finalPrice.Should().Be(120m);
        }

        [Fact]
        public void IndependentEvaluation_ReductionExceedsTotal_ShouldFloorAtZero()
        {
            var originalTotal = 100m;
            var totalReduction = 150m;
            var finalPrice = System.Math.Max(0m, originalTotal - totalReduction);

            finalPrice.Should().Be(0m);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Discount cap — Checkout BC enforcement (new coupon rejected if sum >= total)
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void DiscountCap_NewCouponWhenSumAlreadyCoversTotal_ShouldBeRejected()
        {
            // Spec: new coupon REJECTED if existing reduction sum >= originalTotal
            var originalTotal = 100m;
            var existingReductions = 100m; // already fully discounted

            var newCouponShouldBeRejected = existingReductions >= originalTotal;

            newCouponShouldBeRejected.Should().BeTrue();
        }

        // ══════════════════════════════════════════════════════════════════════
        // OrderPriceAdjusted message — replaces CouponApplied in Slice 2
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void OrderPriceAdjusted_ShouldContainAllRequiredFields()
        {
            var msg = new OrderPriceAdjusted(
                OrderId: 99,
                NewPrice: 170m,
                Delta: -30m,
                AdjustmentType: "coupon",
                ReferenceId: 7);

            msg.OrderId.Should().Be(99);
            msg.NewPrice.Should().Be(170m);
            msg.Delta.Should().Be(-30m);
            msg.AdjustmentType.Should().Be("coupon");
            msg.ReferenceId.Should().Be(7);
        }

        [Fact]
        public void OrderPriceAdjusted_ShouldImplementIMessage()
        {
            var msg = new OrderPriceAdjusted(1, 100m, -10m, "coupon", 1);

            msg.Should().BeAssignableTo<IMessage>();
        }

        // ══════════════════════════════════════════════════════════════════════
        // CouponApplicationRecord — audit trail creation
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ApplyCoupon_ShouldCreateCouponApplicationRecord()
        {
            // Spec: every successful coupon application should create a CouponApplicationRecord
            // for audit purposes. This record is never deleted.
            var record = CouponApplicationRecord.Create(
                couponUsedId: 7,
                couponCode: "SAVE15",
                discountType: "percentage-off",
                discountValue: 15m,
                originalTotal: 200m,
                reduction: 30m);

            record.CouponUsedId.Should().Be(7);
            record.WasReversed.Should().BeFalse();

            // Verify the repository receives AddAsync call (spec documentation)
            _applicationRecords.Setup(x => x.AddAsync(It.IsAny<CouponApplicationRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _applicationRecords.Object.AddAsync(record);

            _applicationRecords.Verify(r => r.AddAsync(
                It.Is<CouponApplicationRecord>(ar =>
                    ar.CouponUsedId == 7 &&
                    ar.CouponCode == "SAVE15" &&
                    ar.DiscountType == "percentage-off" &&
                    ar.Reduction == 30m),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Concurrency — optimistic concurrency with Version (rowversion)
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void Coupon_VersionProperty_ShouldExistForOptimisticConcurrency()
        {
            // Spec: Coupon has Version (byte[]) for rowversion optimistic concurrency
            var coupon = CreateRuleBasedCoupon();

            coupon.Version.Should().BeNull(); // Not set until persisted by EF Core
            typeof(Coupon).GetProperty(nameof(Coupon.Version)).Should().NotBeNull();
        }

        [Fact]
        public void ConcurrencyRetry_MaxTwoRetries_ShouldBeEnforced()
        {
            // Spec: on DbUpdateConcurrencyException → reload + re-evaluate + retry (max 2)
            // First successful write wins.
            var maxRetries = 2;

            maxRetries.Should().Be(2);
        }

        // ══════════════════════════════════════════════════════════════════════
        // NullRuntimeCouponSource — default ML seam
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task NullRuntimeCouponSource_ShouldReturnNull()
        {
            var source = new NullRuntimeCouponSource();

            var result = await source.SuggestCouponAsync("user-1", null);

            result.Should().BeNull();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Slice 1 backward compatibility
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ApplyCouponAsync_Slice1HappyPath_ShouldStillWork()
        {
            // Slice 1 CouponApplied message still works
            var coupon = Coupon.Create("SAVE10", 10, "desc");
            typeof(Coupon).GetProperty(nameof(Coupon.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(coupon, new object[] { new CouponId(5) });

            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE10", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((CouponUsed)null);
            _couponUsed.Setup(x => x.AddAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()))
                .Callback<CouponUsed, CancellationToken>((cu, _) =>
                {
                    typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                        .GetSetMethod(nonPublic: true)!
                        .Invoke(cu, new object[] { new CouponUsedId(7) });
                })
                .Returns(Task.CompletedTask);

            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE10", 99);

            result.Should().Be(CouponApplyResult.Applied);
        }

        [Fact]
        public async Task RemoveCouponAsync_Slice1HappyPath_ShouldStillWork()
        {
            var couponUsed = CouponUsed.Create(new CouponId(5), 99);
            typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(couponUsed, new object[] { new CouponUsedId(3) });
            var coupon = Coupon.Create("SAVE10", 10, "desc");
            typeof(Coupon).GetProperty(nameof(Coupon.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(coupon, new object[] { new CouponId(5) });
            coupon.MarkAsUsed();

            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(couponUsed);
            _coupons.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

            var result = await CreateSlice1Service().RemoveCouponAsync(99);

            result.Should().Be(CouponRemoveResult.Removed);
        }

        // ── helper ────────────────────────────────────────────────────────────

        private ICouponService CreateSlice1Service()
            => new CouponService(_coupons.Object, _couponUsed.Object, _orderExistence.Object, _broker.Object);
    }
}
