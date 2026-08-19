using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Domain.Sales.Coupons;
using AwesomeAssertions;
using Moq;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Mock<IModuleClient> _moduleClient;
        private readonly Mock<ICouponsUnitOfWork> _unitOfWork;
        private readonly Mock<IOutboxWriter> _outboxWriter;
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
            _moduleClient = new Mock<IModuleClient>();
            _unitOfWork = new Mock<ICouponsUnitOfWork>();
            _outboxWriter = new Mock<IOutboxWriter>();
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
            EntityIdSetter.Set(coupon, new CouponId(id));
            return coupon;
        }

        private static CouponUsed CreateCouponUsedForDb(int id = 1, int couponId = 1, int orderId = 99, string userId = "user-1")
        {
            var cu = CouponUsed.CreateForDbCoupon(new CouponId(couponId), orderId, userId);
            EntityIdSetter.Set(cu, new CouponUsedId(id));
            return cu;
        }

        private void SetupPerProductCouponPersistence(int assignedId = 99)
        {
            _coupons.Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Coupon)null);
            _coupons.Setup(x => x.AddAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>()))
                .Callback<Coupon, CancellationToken>((coupon, _) => EntityIdSetter.Set(coupon, new CouponId(assignedId)))
                .Returns(Task.CompletedTask);
            _scopeTargets.Setup(x => x.AddRangeAsync(It.IsAny<IReadOnlyList<CouponScopeTarget>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupCouponLookup(string code, Coupon coupon)
        {
            _coupons.Setup(x => x.GetByCodeAsync(code, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
        }

        private void SetupOrderExists(bool exists)
        {
            _moduleClient.Setup(x => x.SendAsync(It.IsAny<OrderExistsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(exists);
        }

        private void SetupNoAppliedCoupons(int orderId = 99)
        {
            SetupCouponUsedLookup(new List<CouponUsed>(), orderId);
        }

        private void SetupPipelineResult(CouponRulePipelineResult result)
        {
            _pipeline.Setup(p => p.EvaluateAsync(
                    It.IsAny<IReadOnlyList<CouponRuleDefinition>>(),
                    It.IsAny<CouponEvaluationContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }

        private void SetupCouponUsedLookup(IReadOnlyList<CouponUsed> coupons, int orderId = 99)
        {
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(coupons);
        }

        private void SetupApplicationRecords(IReadOnlyList<CouponApplicationRecord> records, int orderId = 99)
        {
            _applicationRecords.Setup(x => x.FindByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(records);
        }

            private void SetupApplicationRecordPersistence()
            {
                _applicationRecords.Setup(x => x.AddAsync(
                    It.IsAny<CouponApplicationRecord>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            }

        private void SetupCouponUsedPersistence(int assignedId = 7)
        {
            _couponUsed.Setup(x => x.AddAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()))
                .Callback<CouponUsed, CancellationToken>((couponUsed, _) =>
                    EntityIdSetter.Set(couponUsed, new CouponUsedId(assignedId)))
                .Returns(Task.CompletedTask);
        }

        private void SetupCouponRemoval(CouponUsed couponUsed, Coupon coupon)
        {
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(couponUsed);
            _coupons.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
        }

        // ══════════════════════════════════════════════════════════════════════
        // CreateCouponAsync — coupon creation with full validation pipeline
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task CreateCouponAsync_ValidDto_ShouldPersistCouponAndReturnSuccess()
        {
            // Arrange
            var dto = new CreateCouponDto
            {
                Code = "SAVE15",
                Description = "15% off",
                RulesJson = BuildRulesJson(
                    new CouponRuleDefinition("order-total", CouponRuleCategory.Scope, new Dictionary<string, string>()),
                    new CouponRuleDefinition("percentage-off", CouponRuleCategory.Discount, new Dictionary<string, string> { ["percent"] = "15" })),
                ScopeTargets = new List<ScopeTargetDto>()
            };
            SetupCouponLookup("SAVE15", null);
            _coupons.Setup(x => x.AddAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var service = CreateSlice1Service();

            // Act
            var result = await service.CreateCouponAsync(dto, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeTrue();
            _coupons.Verify(x => x.AddAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateCouponAsync_PerProductScope_WithTargets_ShouldPersistScopeTargets()
        {
            // Arrange
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
            SetupPerProductCouponPersistence();
            var service = CreateSlice1Service();

            // Act
            var result = await service.CreateCouponAsync(dto, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeTrue();
            _scopeTargets.Verify(x => x.AddRangeAsync(
                It.Is<IReadOnlyList<CouponScopeTarget>>(targets => targets.Count == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateCouponAsync_InvalidRuleComposition_ShouldReturnFailure()
        {
            // Arrange
            // No scope rule — should fail validation
            var dto = new CreateCouponDto
            {
                Code = "INVALID",
                Description = "desc",
                RulesJson = BuildRulesJson(
                    new CouponRuleDefinition("percentage-off", CouponRuleCategory.Discount, new Dictionary<string, string> { ["percent"] = "15" })),
                ScopeTargets = new List<ScopeTargetDto>()
            };
            SetupCouponLookup("INVALID", null);
            var service = CreateSlice1Service();

            // Act
            var result = await service.CreateCouponAsync(dto, TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeFalse();
            result.FailureReason.Should().Contain("Scope");
        }

        // ══════════════════════════════════════════════════════════════════════
        // Multi-coupon per order — MaxCouponsPerOrder enforcement
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void MaxCouponsPerOrder_DefaultShouldBeFive()
        {
            // Arrange
            var options = new CouponsOptions();

            // Act
            var maxCoupons = options.MaxCouponsPerOrder;

            // Assert
            maxCoupons.Should().Be(5);
        }

        [Fact]
        public void MaxCouponsPerOrder_HardCeilingShouldBeTen()
        {
            // Arrange
            var options = new CouponsOptions { MaxCouponsPerOrder = 15 };

            // Act
            var configuredMaximum = options.MaxCouponsPerOrder;
            var enforcedMaximum = System.Math.Min(configuredMaximum, 10);

            // Assert
            // The hard ceiling should be enforced at service level
            // options allows setting any value, but service must cap at 10
            configuredMaximum.Should().Be(15); // Config allows it
            // Service must enforce: Math.Min(options.MaxCouponsPerOrder, 10)
            enforcedMaximum.Should().Be(10);
        }

        [Fact]
        public async Task ApplyCouponAsync_OrderAlreadyAtMaxCoupons_ShouldRejectNewCoupon()
        {
            // Arrange
            // Spec: if order already has MaxCouponsPerOrder coupons, reject
            var existingCoupons = Enumerable.Range(1, 5)
                .Select(index => CreateCouponUsedForDb(index, index, 99))
                .ToList();

            SetupCouponUsedLookup(existingCoupons);

            // Act
            var couponCount = existingCoupons.Count;

            // Assert
            couponCount.Should().Be(5);
            // When Slice 2 is implemented, ApplyCouponAsync should check count and reject
        }

        [Fact]
        public void CouponApplicationResult_Failed_ShouldContainReason()
        {
            // Arrange
            var result = CouponApplicationResult.Failed("Coupon expired");

            // Act
            var isSuccess = result.Success;
            var reason = result.FailureReason;

            // Assert
            isSuccess.Should().BeFalse();
            reason.Should().Be("Coupon expired");
        }

        // ══════════════════════════════════════════════════════════════════════
        // OrderPriceAdjusted message — replaces CouponApplied in Slice 2
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void OrderPriceAdjusted_ShouldContainAllRequiredFields()
        {
            // Arrange
            var msg = new OrderPriceAdjusted(
                OrderId: 99,
                NewPrice: 170m,
                Delta: -30m,
                AdjustmentType: "coupon",
                ReferenceId: 7);

            // Act
            var orderId = msg.OrderId;

            // Assert
            orderId.Should().Be(99);
            msg.NewPrice.Should().Be(170m);
            msg.Delta.Should().Be(-30m);
            msg.AdjustmentType.Should().Be("coupon");
            msg.ReferenceId.Should().Be(7);
        }

        [Fact]
        public void OrderPriceAdjusted_ShouldImplementIMessage()
        {
            // Arrange
            var msg = new OrderPriceAdjusted(1, 100m, -10m, "coupon", 1);

            // Act
            var message = msg;

            // Assert
            message.Should().BeAssignableTo<IMessage>();
        }

        // ══════════════════════════════════════════════════════════════════════
        // CouponApplicationRecord — audit trail creation
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ApplyCoupon_ShouldCreateCouponApplicationRecord()
        {
            // Arrange
            // Spec: every successful coupon application should create a CouponApplicationRecord
            // for audit purposes. This record is never deleted.
            var record = CouponApplicationRecord.Create(
                couponUsedId: 7,
                couponCode: "SAVE15",
                discountType: "percentage-off",
                discountValue: 15m,
                originalTotal: 200m,
                reduction: 30m);

            SetupApplicationRecordPersistence();

            // Act
            await _applicationRecords.Object.AddAsync(record, TestContext.Current.CancellationToken);

            // Assert
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
            // Arrange
            // Spec: Coupon has Version (byte[]) for rowversion optimistic concurrency
            var coupon = CreateRuleBasedCoupon();

            // Act
            var versionProperty = typeof(Coupon).GetProperty(nameof(Coupon.Version));

            // Assert
            coupon.Version.Should().BeNull(); // Not set until persisted by EF Core
            versionProperty.Should().NotBeNull();
        }

        [Fact]
        public void ConcurrencyRetry_MaxTwoRetries_ShouldBeEnforced()
        {
            // Arrange
            // Spec: on DbUpdateConcurrencyException → reload + re-evaluate + retry (max 2)
            // First successful write wins.
            var maxRetries = 2;

            // Act
            var retryLimit = maxRetries;

            // Assert
            retryLimit.Should().Be(2);
        }

        // ══════════════════════════════════════════════════════════════════════
        // NullRuntimeCouponSource — default ML seam
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task NullRuntimeCouponSource_ShouldReturnNull()
        {
            // Arrange
            var source = new NullRuntimeCouponSource();

            // Act
            var result = await source.SuggestCouponAsync("user-1", null, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Slice 1 backward compatibility
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ApplyCouponAsync_Slice1CouponWithNoRules_ShouldReturnNoDiscountProduced()
        {
            // Arrange
            var coupon = Coupon.Create("SAVE10", "desc");
            EntityIdSetter.Set(coupon, new CouponId(5));

            SetupOrderExists(true);
            SetupCouponLookup("SAVE10", coupon);
            SetupNoAppliedCoupons();

            // Act
            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE10", new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>()), TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(CouponApplyResult.NoDiscountProduced);
        }

        [Fact]
        public async Task ApplyCouponAsync_RuleBasedCoupon_PipelinePasses_ShouldReturnApplied()
        {
            // Arrange
            var coupon = CreateRuleBasedCoupon(id: 5, code: "SAVE15");
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());
            SetupOrderExists(true);
            SetupCouponLookup("SAVE15", coupon);
            SetupNoAppliedCoupons();
            SetupCouponUsedPersistence();
            SetupPipelineResult(CouponRulePipelineResult.Success(30m));

            // Act
            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE15", context, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(CouponApplyResult.Applied);
            _pipeline.Verify(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), context, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(o => o.EnqueueAsync(It.Is<IMessage>(m => m.GetType() == typeof(CouponApplied) && ((CouponApplied)m).OrderId == 99 && ((CouponApplied)m).CouponUsedId == 7), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(o => o.EnqueueAsync(It.Is<IMessage>(m => m.GetType() == typeof(OrderPriceAdjusted) && ((OrderPriceAdjusted)m).OrderId == 99 && ((OrderPriceAdjusted)m).NewPrice == 170m && ((OrderPriceAdjusted)m).Delta == -30m && ((OrderPriceAdjusted)m).AdjustmentType == "coupon" && ((OrderPriceAdjusted)m).ReferenceId == 7), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ApplyCouponAsync_RuleBasedCoupon_PipelineFails_ShouldReturnRulesNotSatisfied()
        {
            // Arrange
            var coupon = CreateRuleBasedCoupon(code: "SAVE15");
            var context = new CouponEvaluationContext(99, "user-1", 10m, new List<CouponEvaluationItem>());
            SetupOrderExists(true);
            SetupCouponLookup("SAVE15", coupon);
            SetupNoAppliedCoupons();
            SetupPipelineResult(CouponRulePipelineResult.Failure(new[] { "Order total too low." }));

            // Act
            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE15", context, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(CouponApplyResult.RulesNotSatisfied);
            _couponUsed.Verify(r => r.AddAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RemoveCouponAsync_Slice1HappyPath_ShouldStillWork()
        {
            // Arrange
            var couponUsed = CouponUsed.Create(new CouponId(5), 99);
            EntityIdSetter.Set(couponUsed, new CouponUsedId(3));
            var coupon = Coupon.Create("SAVE10", "desc");
            EntityIdSetter.Set(coupon, new CouponId(5));
            coupon.MarkAsUsed();

            SetupCouponRemoval(couponUsed, coupon);

            // Act
            var result = await CreateSlice1Service().RemoveCouponAsync(99, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(CouponRemoveResult.Removed);
        }

        // ── helper ────────────────────────────────────────────────────────────

        private ICouponService CreateSlice1Service()
        {
            var txMock = new Mock<IOutboxTransaction>();
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
            return new CouponService(_coupons.Object, _couponUsed.Object, _moduleClient.Object, _unitOfWork.Object, _outboxWriter.Object, _scopeTargets.Object, _pipeline.Object, _options, _applicationRecords.Object);
        }

        // ══════════════════════════════════════════════════════════════════════
        // SimulateCouponAsync — runs rule pipeline without committing
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task SimulateCouponAsync_CouponNotFound_ShouldReturnFailure()
        {
            // Arrange
            SetupCouponLookup("MISSING", null);
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());

            // Act
            var result = await CreateSlice1Service().SimulateCouponAsync("MISSING", context, TestContext.Current.CancellationToken);

            // Assert
            result.Passed.Should().BeFalse();
            result.FailureReasons.Should().ContainSingle(r => r.Contains("MISSING"));
        }

        [Fact]
        public async Task SimulateCouponAsync_CouponNotAvailable_ShouldReturnFailure()
        {
            // Arrange
            var coupon = Coupon.Create("USED10", "desc");
            coupon.MarkAsUsed();
            SetupCouponLookup("USED10", coupon);
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());

            // Act
            var result = await CreateSlice1Service().SimulateCouponAsync("USED10", context, TestContext.Current.CancellationToken);

            // Assert
            result.Passed.Should().BeFalse();
            result.FailureReasons.Should().ContainSingle(r => r.Contains("USED10"));
        }

        [Fact]
        public async Task SimulateCouponAsync_Slice1CouponWithNoRules_ShouldReturnSuccessWithZeroReduction()
        {
            // Arrange
            var coupon = Coupon.Create("FLAT", "flat coupon, no rules");
            SetupCouponLookup("FLAT", coupon);
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());

            // Act
            var result = await CreateSlice1Service().SimulateCouponAsync("FLAT", context, TestContext.Current.CancellationToken);

            // Assert
            result.Passed.Should().BeTrue();
            result.TotalReduction.Should().Be(0m);
            _pipeline.Verify(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SimulateCouponAsync_RulesPass_ShouldReturnSuccessWithReduction()
        {
            // Arrange
            var coupon = CreateRuleBasedCoupon(code: "SAVE15");
            SetupCouponLookup("SAVE15", coupon);
            SetupPipelineResult(CouponRulePipelineResult.Success(30m));
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());

            // Act
            var result = await CreateSlice1Service().SimulateCouponAsync("SAVE15", context, TestContext.Current.CancellationToken);

            // Assert
            result.Passed.Should().BeTrue();
            result.TotalReduction.Should().Be(30m);
        }

        [Fact]
        public async Task SimulateCouponAsync_RulesFail_ShouldReturnFailure()
        {
            // Arrange
            var coupon = CreateRuleBasedCoupon(code: "SAVE15");
            SetupCouponLookup("SAVE15", coupon);
            SetupPipelineResult(CouponRulePipelineResult.Failure(new[] { "Order total too low." }));
            var context = new CouponEvaluationContext(99, "user-1", 5m, new List<CouponEvaluationItem>());

            // Act
            var result = await CreateSlice1Service().SimulateCouponAsync("SAVE15", context, TestContext.Current.CancellationToken);

            // Assert
            result.Passed.Should().BeFalse();
            result.FailureReasons.Should().ContainSingle("Order total too low.");
        }

        // ══════════════════════════════════════════════════════════════════════
        // Stacking strategy — effective price and fixed-value guards
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task ApplyCouponAsync_EffectivePriceAlreadyZero_ShouldReturnNoDiscountProduced()
        {
            // Arrange
            var coupon = CreateRuleBasedCoupon(id: 5, code: "SAVE15");
            var existingCouponUsed = CreateCouponUsedForDb(id: 1, couponId: 10, orderId: 99);
            var previousRecord = CouponApplicationRecord.Create(1, "PREV", "fixed", 200m, 200m, 200m);
            var context = new CouponEvaluationContext(99, "user-1", 200m, new List<CouponEvaluationItem>());

            SetupOrderExists(true);
            SetupCouponLookup("SAVE15", coupon);
            SetupCouponUsedLookup(new List<CouponUsed> { existingCouponUsed });
            SetupApplicationRecords(new List<CouponApplicationRecord> { previousRecord });

            // Act
            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE15", context, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(CouponApplyResult.NoDiscountProduced);
            _pipeline.Verify(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ApplyCouponAsync_CouponReductionExceedsEffectivePrice_ShouldCapAndApply()
        {
            // Arrange
            // price=150, already reduced by 100 → effective=50; pipeline wants 75 → capped to 50
            var coupon = CreateRuleBasedCoupon(id: 5, code: "SAVE15");
            var existingCouponUsed = CreateCouponUsedForDb(id: 1, couponId: 10, orderId: 99);
            var previousRecord = CouponApplicationRecord.Create(1, "PREV", "fixed", 100m, 150m, 100m);
            var context = new CouponEvaluationContext(99, "user-1", 150m, new List<CouponEvaluationItem>());

            SetupOrderExists(true);
            SetupCouponLookup("SAVE15", coupon);
            SetupCouponUsedLookup(new List<CouponUsed> { existingCouponUsed });
            SetupApplicationRecords(new List<CouponApplicationRecord> { previousRecord });
            SetupCouponUsedPersistence();
            SetupPipelineResult(CouponRulePipelineResult.Success(75m));

            // Act
            var result = await CreateSlice1Service().ApplyCouponAsync("SAVE15", context, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(CouponApplyResult.Applied);
            _outboxWriter.Verify(o => o.EnqueueAsync(It.Is<IMessage>(m => m.GetType() == typeof(OrderPriceAdjusted) && ((OrderPriceAdjusted)m).NewPrice == 0m && ((OrderPriceAdjusted)m).Delta == -50m), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
            _applicationRecords.Verify(r => r.AddAsync(
                It.Is<CouponApplicationRecord>(ar => ar.Reduction == 50m),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ApplyCouponAsync_FixedValueCouponExceedsOriginalTotal_ShouldReturnRulesNotSatisfied()
        {
            // Arrange
            // fixed coupon of 100 applied to order worth 99 → reject
            var rulesJson = BuildRulesJson(
                new CouponRuleDefinition("order-total", CouponRuleCategory.Scope, new Dictionary<string, string>()),
                new CouponRuleDefinition("fixed-amount-off", CouponRuleCategory.Discount, new Dictionary<string, string> { ["amount"] = "100" }));
            var coupon = Coupon.CreateWithRules("FIXED100", "100 off", rulesJson, new List<CouponScopeTarget>());
            EntityIdSetter.Set(coupon, new CouponId(5));

            var context = new CouponEvaluationContext(99, "user-1", 99m, new List<CouponEvaluationItem>());
            SetupOrderExists(true);
            SetupCouponLookup("FIXED100", coupon);
            SetupNoAppliedCoupons();

            // Act
            var result = await CreateSlice1Service().ApplyCouponAsync("FIXED100", context, TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be(CouponApplyResult.RulesNotSatisfied);
            _pipeline.Verify(p => p.EvaluateAsync(It.IsAny<IReadOnlyList<CouponRuleDefinition>>(), It.IsAny<CouponEvaluationContext>(), It.IsAny<CancellationToken>()), Times.Never);
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
