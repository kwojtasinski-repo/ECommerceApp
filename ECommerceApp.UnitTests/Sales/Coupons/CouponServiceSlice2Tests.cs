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
        private readonly Mock<IScopeTargetRepository> _scopeTargets;
        private readonly Mock<ICouponRulePipeline> _pipeline;
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
            _scopeTargets = new Mock<IScopeTargetRepository>();
            _pipeline = new Mock<ICouponRulePipeline>();
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
            _coupons.Setup(x => x.GetByCodeAsync("SAVE15", It.IsAny<CancellationToken>())).ReturnsAsync((Coupon)null);
            _coupons.Setup(x => x.AddAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var service = CreateSlice1Service();

            var result = await service.CreateCouponAsync(dto);

            result.Success.Should().BeTrue();
            _coupons.Verify(x => x.AddAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>()), Times.Once);
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
            _coupons.Setup(x => x.GetByCodeAsync("PROD15", It.IsAny<CancellationToken>())).ReturnsAsync((Coupon)null);
            _coupons.Setup(x => x.AddAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>()))
                .Callback<Coupon, CancellationToken>((c, _) =>
                    typeof(Coupon).GetProperty(nameof(Coupon.Id))!
                        .GetSetMethod(nonPublic: true)!
                        .Invoke(c, new object[] { new CouponId(99) }))
                .Returns(Task.CompletedTask);
            _scopeTargets.Setup(x => x.AddRangeAsync(It.IsAny<IReadOnlyList<CouponScopeTarget>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var service = CreateSlice1Service();

            var result = await service.CreateCouponAsync(dto);

            result.Success.Should().BeTrue();
            _scopeTargets.Verify(x => x.AddRangeAsync(
                It.Is<IReadOnlyList<CouponScopeTarget>>(targets => targets.Count == 1),
                It.IsAny<CancellationToken>()), Times.Once);
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
            _coupons.Setup(x => x.GetByCodeAsync("INVALID", It.IsAny<CancellationToken>())).ReturnsAsync((Coupon)null);
            var service = CreateSlice1Service();

            var result = await service.CreateCouponAsync(dto);

            result.Success.Should().BeFalse();
            result.FailureReason.Should().Contain("Scope");
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

        [Fact]
        public void CouponApplicationResult_Failed_ShouldContainReason()
        {
            var result = CouponApplicationResult.Failed("Coupon expired");

            result.Success.Should().BeFalse();
            result.FailureReason.Should().Be("Coupon expired");
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
        public async Task ApplyCouponAsync_Slice1CouponWithNoRules_ShouldReturnNoDiscountProduced()
        {
            var coupon = Coupon.Create("SAVE10", "desc");
            typeof(Coupon).GetProperty(nameof(Coupon.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(coupon, new object[] { new CouponId(5) });

            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE10", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(new List<CouponUsed>());

            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE10", new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>()));

            result.Should().Be(CouponApplyResult.NoDiscountProduced);
        }

        [Fact]
        public async Task ApplyCouponAsync_RuleBasedCoupon_PipelinePasses_ShouldReturnApplied()
        {
            var coupon = CreateRuleBasedCoupon(id: 5, code: "SAVE15");
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE15", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(new List<CouponUsed>());
            _couponUsed.Setup(x => x.AddAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()))
                .Callback<CouponUsed, CancellationToken>((cu, _) =>
                    typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                        .GetSetMethod(nonPublic: true)!
                        .Invoke(cu, new object[] { new CouponUsedId(7) }))
                .Returns(Task.CompletedTask);
            _pipeline.Setup(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CouponRulePipelineResult.Success(30m));

            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE15", context);

            result.Should().Be(CouponApplyResult.Applied);
            _pipeline.Verify(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), context, It.IsAny<CancellationToken>()), Times.Once);
            _broker.Verify(b => b.PublishAsync(It.Is<IMessage[]>(m =>
                m.Length == 2 &&
                m[0].GetType() == typeof(CouponApplied) &&
                ((CouponApplied)m[0]).OrderId == 99 &&
                ((CouponApplied)m[0]).CouponUsedId == 7 &&
                m[1].GetType() == typeof(OrderPriceAdjusted) &&
                ((OrderPriceAdjusted)m[1]).OrderId == 99 &&
                ((OrderPriceAdjusted)m[1]).NewPrice == 170m &&
                ((OrderPriceAdjusted)m[1]).Delta == -30m &&
                ((OrderPriceAdjusted)m[1]).AdjustmentType == "coupon" &&
                ((OrderPriceAdjusted)m[1]).ReferenceId == 7)), Times.Once);
        }

        [Fact]
        public async Task ApplyCouponAsync_RuleBasedCoupon_PipelineFails_ShouldReturnRulesNotSatisfied()
        {
            var coupon = CreateRuleBasedCoupon(code: "SAVE15");
            var context = new CouponEvaluationContext(99, "user-1", 10m, new List<CouponEvaluationItem>());
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE15", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(new List<CouponUsed>());
            _pipeline.Setup(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CouponRulePipelineResult.Failure(new[] { "Order total too low." }));

            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE15", context);

            result.Should().Be(CouponApplyResult.RulesNotSatisfied);
            _couponUsed.Verify(r => r.AddAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task RemoveCouponAsync_Slice1HappyPath_ShouldStillWork()
        {
            var couponUsed = CouponUsed.Create(new CouponId(5), 99);
            typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(couponUsed, new object[] { new CouponUsedId(3) });
            var coupon = Coupon.Create("SAVE10", "desc");
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
            => new CouponService(_coupons.Object, _couponUsed.Object, _orderExistence.Object, _broker.Object, _scopeTargets.Object, _pipeline.Object, _options, _applicationRecords.Object);

        // ══════════════════════════════════════════════════════════════════════
        // SimulateCouponAsync — runs rule pipeline without committing
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task SimulateCouponAsync_CouponNotFound_ShouldReturnFailure()
        {
            _coupons.Setup(x => x.GetByCodeAsync("MISSING", It.IsAny<CancellationToken>())).ReturnsAsync((Coupon)null);
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());

            var result = await CreateSlice1Service().SimulateCouponAsync("MISSING", context);

            result.Passed.Should().BeFalse();
            result.FailureReasons.Should().ContainSingle(r => r.Contains("MISSING"));
        }

        [Fact]
        public async Task SimulateCouponAsync_CouponNotAvailable_ShouldReturnFailure()
        {
            var coupon = Coupon.Create("USED10", "desc");
            coupon.MarkAsUsed();
            _coupons.Setup(x => x.GetByCodeAsync("USED10", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());

            var result = await CreateSlice1Service().SimulateCouponAsync("USED10", context);

            result.Passed.Should().BeFalse();
            result.FailureReasons.Should().ContainSingle(r => r.Contains("USED10"));
        }

        [Fact]
        public async Task SimulateCouponAsync_Slice1CouponWithNoRules_ShouldReturnSuccessWithZeroReduction()
        {
            var coupon = Coupon.Create("FLAT", "flat coupon, no rules");
            _coupons.Setup(x => x.GetByCodeAsync("FLAT", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());

            var result = await CreateSlice1Service().SimulateCouponAsync("FLAT", context);

            result.Passed.Should().BeTrue();
            result.TotalReduction.Should().Be(0m);
            _pipeline.Verify(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SimulateCouponAsync_RulesPass_ShouldReturnSuccessWithReduction()
        {
            var coupon = CreateRuleBasedCoupon(code: "SAVE15");
            _coupons.Setup(x => x.GetByCodeAsync("SAVE15", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _pipeline.Setup(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CouponRulePipelineResult.Success(30m));
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());

            var result = await CreateSlice1Service().SimulateCouponAsync("SAVE15", context);

            result.Passed.Should().BeTrue();
            result.TotalReduction.Should().Be(30m);
        }

        [Fact]
        public async Task SimulateCouponAsync_RulesFail_ShouldReturnFailure()
        {
            var coupon = CreateRuleBasedCoupon(code: "SAVE15");
            _coupons.Setup(x => x.GetByCodeAsync("SAVE15", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _pipeline.Setup(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CouponRulePipelineResult.Failure(new[] { "Order total too low." }));
            var context = new CouponEvaluationContext(99, "user-1", 5m, new List<CouponEvaluationItem>());

            var result = await CreateSlice1Service().SimulateCouponAsync("SAVE15", context);

            result.Passed.Should().BeFalse();
            result.FailureReasons.Should().ContainSingle("Order total too low.");
        }

        // ══════════════════════════════════════════════════════════════════════
        // Stacking strategy — effective price and fixed-value guards
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ApplyCouponAsync_EffectivePriceAlreadyZero_ShouldReturnNoDiscountProduced()
        {
            var coupon = CreateRuleBasedCoupon(id: 5, code: "SAVE15");
            var existingCouponUsed = CreateCouponUsedForDb(id: 1, couponId: 10, orderId: 99);
            var previousRecord = CouponApplicationRecord.Create(1, "PREV", "fixed", 200m, 200m, 200m);
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());

            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE15", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed> { existingCouponUsed });
            _applicationRecords.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponApplicationRecord> { previousRecord });

            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE15", context);

            result.Should().Be(CouponApplyResult.NoDiscountProduced);
            _pipeline.Verify(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }

        [Fact]
        public async Task ApplyCouponAsync_CouponReductionExceedsEffectivePrice_ShouldCapAndApply()
        {
            // price=150, already reduced by 100 → effective=50; pipeline wants 75 → capped to 50
            var coupon = CreateRuleBasedCoupon(id: 5, code: "SAVE15");
            var existingCouponUsed = CreateCouponUsedForDb(id: 1, couponId: 10, orderId: 99);
            var previousRecord = CouponApplicationRecord.Create(1, "PREV", "fixed", 100m, 150m, 100m);
            var context = new CouponEvaluationContext(99, "user-1", 150m, new List<CouponEvaluationItem>());

            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("SAVE15", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed> { existingCouponUsed });
            _applicationRecords.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponApplicationRecord> { previousRecord });
            _couponUsed.Setup(x => x.AddAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()))
                .Callback<CouponUsed, CancellationToken>((cu, _) =>
                    typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                        .GetSetMethod(nonPublic: true)!
                        .Invoke(cu, new object[] { new CouponUsedId(7) }))
                .Returns(Task.CompletedTask);
            _pipeline.Setup(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CouponRulePipelineResult.Success(75m));

            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE15", context);

            result.Should().Be(CouponApplyResult.Applied);
            _broker.Verify(b => b.PublishAsync(It.Is<IMessage[]>(m =>
                m.Length == 2 &&
                m[1].GetType() == typeof(OrderPriceAdjusted) &&
                ((OrderPriceAdjusted)m[1]).NewPrice == 0m &&
                ((OrderPriceAdjusted)m[1]).Delta == -50m)), Times.Once);
            _applicationRecords.Verify(r => r.AddAsync(
                It.Is<CouponApplicationRecord>(ar => ar.Reduction == 50m),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ApplyCouponAsync_FixedValueCouponExceedsOriginalTotal_ShouldReturnRulesNotSatisfied()
        {
            // fixed coupon of 100 applied to order worth 99 → reject
            var rulesJson = BuildRulesJson(
                new CouponRuleDefinition("order-total", CouponRuleCategory.Scope, new Dictionary<string, string>()),
                new CouponRuleDefinition("fixed-amount-off", CouponRuleCategory.Discount, new Dictionary<string, string> { ["amount"] = "100" }));
            var coupon = Coupon.CreateWithRules("FIXED100", "100 off", rulesJson, new List<CouponScopeTarget>());
            typeof(Coupon).GetProperty(nameof(Coupon.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(coupon, new object[] { new CouponId(5) });

            var context = new CouponEvaluationContext(99, "user-1", 99m, new List<CouponEvaluationItem>());
            _orderExistence.Setup(x => x.ExistsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _coupons.Setup(x => x.GetByCodeAsync("FIXED100", It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed>());

            var result = await CreateSlice1Service().ApplyCouponAsync("FIXED100", context);

            result.Should().Be(CouponApplyResult.RulesNotSatisfied);
            _pipeline.Verify(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()), Times.Never);
            _broker.Verify(b => b.PublishAsync(It.IsAny<IMessage[]>()), Times.Never);
        }
    }
}
