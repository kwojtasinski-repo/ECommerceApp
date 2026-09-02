using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons;
using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Domain.Sales.Coupons;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponUpdateRulesTests
    {
        private readonly Mock<ICouponRepository> _coupons = new();
        private readonly Mock<IScopeTargetRepository> _scopeTargets = new();
        private readonly Mock<ICouponRuleRegistry> _registry = new();
        private readonly Mock<ICouponRulePipeline> _pipeline = new();
        private readonly Mock<ICouponUsedRepository> _couponUsed = new();
        private readonly Mock<IModuleClient> _moduleClient = new();
        private readonly Mock<ICouponsUnitOfWork> _unitOfWork = new();
        private readonly Mock<IOutboxWriter> _outboxWriter = new();
        private readonly Mock<ICouponApplicationRecordRepository> _records = new();

        public CouponUpdateRulesTests()
        {
            _registry
                .Setup(x => x.TryGetRule(It.IsAny<string>(), out It.Ref<CouponRuleDescriptor>.IsAny))
                .Returns((string name, out CouponRuleDescriptor descriptor) =>
                {
                    if (name == "missing-rule")
                    {
                        descriptor = null;
                        return false;
                    }

                    var category = name == CouponRuleNames.OrderTotal || name == CouponRuleNames.PerProduct
                        ? CouponRuleCategory.Scope
                        : CouponRuleCategory.Discount;
                    descriptor = new CouponRuleDescriptor(name, category, null, null, null, null);
                    return true;
                });
            _scopeTargets.Setup(x => x.GetByCouponIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponScopeTarget>());
        }

        [Fact]
        public async Task UpdateCouponAsync_NewRules_ShouldReplaceAndRoundTripRulesJson()
        {
            var coupon = CreateCoupon();
            SetupCoupon(coupon);
            var result = await CreateService().UpdateCouponAsync(FormDto("20"));

            result.Outcome.Should().Be(CouponUpdateOutcome.Updated);
            coupon.GetRules().Should().ContainSingle(rule => rule.Name == CouponRuleNames.PercentageOff && rule.Parameters["percent"] == "20");
        }

        [Fact]
        public async Task UpdateCouponAsync_Category_ShouldComeFromRegistry()
        {
            var coupon = CreateCoupon();
            SetupCoupon(coupon);
            var result = await CreateService().UpdateCouponAsync(FormDto("20"));

            result.Outcome.Should().Be(CouponUpdateOutcome.Updated);
            coupon.GetRules()[0].Category.Should().Be(CouponRuleCategory.Scope);
            coupon.GetRules()[1].Category.Should().Be(CouponRuleCategory.Discount);
        }

        [Fact]
        public async Task UpdateCouponAsync_UnknownRule_ShouldReturnValidationFailure()
        {
            var coupon = CreateCoupon();
            SetupCoupon(coupon);
            var form = FormDto("20");
            form.Rules.Discount.Name = "missing-rule";

            var result = await CreateService().UpdateCouponAsync(form);

            result.Outcome.Should().Be(CouponUpdateOutcome.ValidationFailed);
            _coupons.Verify(x => x.UpdateWithVersionAsync(It.IsAny<Coupon>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateCouponAsync_InvalidParameters_ShouldReturnValidationFailureBeforePersistence()
        {
            var validator = new Mock<ICouponRuleParameterValidator>();
            validator.Setup(x => x.Validate(It.IsAny<IReadOnlyDictionary<string, string>>()))
                .Returns(new[] { "percent is invalid" });
            ConfigureRule(CouponRuleNames.PercentageOff, CouponRuleCategory.Discount, validator.Object);
            var coupon = CreateCoupon();
            SetupCoupon(coupon);

            var result = await CreateService().UpdateCouponAsync(FormDto("invalid"));

            result.Outcome.Should().Be(CouponUpdateOutcome.ValidationFailed);
            _coupons.Verify(x => x.UpdateWithVersionAsync(It.IsAny<Coupon>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateCouponAsync_OrderTotalWithoutTargets_ShouldFailDomainValidation()
        {
            var coupon = CreateCoupon();
            SetupCoupon(coupon);
            var form = FormDto("20");
            form.Rules.Scope.Name = CouponRuleNames.PerProduct;

            var result = await CreateService().UpdateCouponAsync(form);

            result.Outcome.Should().Be(CouponUpdateOutcome.ValidationFailed);
            result.FailureReason.Should().Contain("requires scope targets");
        }

        [Fact]
        public async Task UpdateCouponAsync_PerProductToOrderTotal_ShouldDeleteExistingTargets()
        {
            var coupon = CreateCoupon();
            SetupCoupon(coupon);
            var oldTarget = CouponScopeTarget.Create(coupon.Id, "per-product", 1, "Old");
            _scopeTargets.Setup(x => x.GetByCouponIdAsync(coupon.Id.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponScopeTarget> { oldTarget });

            var result = await CreateService().UpdateCouponAsync(FormDto("20"));

            result.Outcome.Should().Be(CouponUpdateOutcome.Updated);
            _scopeTargets.Verify(x => x.DeleteRangeAsync(It.Is<IReadOnlyList<CouponScopeTarget>>(targets => targets.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateCouponAsync_PerProductTargetChange_ShouldReplaceInsteadOfAppend()
        {
            var coupon = CreateCoupon();
            SetupCoupon(coupon);
            var oldTarget = CouponScopeTarget.Create(coupon.Id, "per-product", 1, "Old");
            _scopeTargets.Setup(x => x.GetByCouponIdAsync(coupon.Id.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponScopeTarget> { oldTarget });
            var form = FormDto("20");
            form.Rules.Scope.Name = CouponRuleNames.PerProduct;
            form.ScopeTargets.Add(new ScopeTargetDto { ScopeType = "per-product", TargetId = 2, TargetName = "New" });

            var result = await CreateService().UpdateCouponAsync(form);

            result.Outcome.Should().Be(CouponUpdateOutcome.Updated);
            _scopeTargets.Verify(x => x.DeleteRangeAsync(It.IsAny<IReadOnlyList<CouponScopeTarget>>(), It.IsAny<CancellationToken>()), Times.Once);
            _scopeTargets.Verify(x => x.AddRangeAsync(It.Is<IReadOnlyList<CouponScopeTarget>>(targets => targets.Count == 1 && targets[0].TargetId == 2), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateCouponAsync_RepositoryConcurrencyFailure_ShouldReturnConflict()
        {
            var coupon = CreateCoupon();
            SetupCoupon(coupon);
            _coupons.Setup(x => x.UpdateWithVersionAsync(coupon, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateConcurrencyException());

            var result = await CreateService().UpdateCouponAsync(FormDto("20"));

            result.Outcome.Should().Be(CouponUpdateOutcome.ConcurrencyConflict);
        }

        [Fact]
        public async Task UpdateCouponAsync_StaleVersion_ShouldReturnConflictBeforePersistence()
        {
            var coupon = CreateCoupon();
            SetVersion(coupon, new byte[] { 1, 2, 3, 4 });
            SetupCoupon(coupon);
            var form = FormDto("20");
            form.Version = new byte[] { 9, 9, 9, 9 };

            var result = await CreateService().UpdateCouponAsync(form);

            result.Outcome.Should().Be(CouponUpdateOutcome.ConcurrencyConflict);
            _coupons.Verify(x => x.UpdateWithVersionAsync(It.IsAny<Coupon>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateCouponAsync_VersionNotPostedBack_ShouldReturnConflictRatherThanOverwrite()
        {
            // A versioned row with nothing posted back means the form did not round-trip it, so there
            // is no way to tell whether someone else has written since — refuse rather than overwrite.
            var coupon = CreateCoupon();
            SetVersion(coupon, new byte[] { 1, 2, 3, 4 });
            SetupCoupon(coupon);
            var form = FormDto("20");
            form.Version = null;

            var result = await CreateService().UpdateCouponAsync(form);

            result.Outcome.Should().Be(CouponUpdateOutcome.ConcurrencyConflict);
            _coupons.Verify(x => x.UpdateWithVersionAsync(It.IsAny<Coupon>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateCouponAsync_MatchingVersion_ShouldPassTheOriginalVersionToTheRepository()
        {
            var version = new byte[] { 1, 2, 3, 4 };
            var coupon = CreateCoupon();
            SetVersion(coupon, version);
            SetupCoupon(coupon);
            var form = FormDto("20");
            form.Version = new byte[] { 1, 2, 3, 4 };

            var result = await CreateService().UpdateCouponAsync(form);

            result.Outcome.Should().Be(CouponUpdateOutcome.Updated);
            // The version the edit began from must reach the repository, otherwise EF compares the row
            // against itself and the database-level concurrency guard never fires.
            _coupons.Verify(x => x.UpdateWithVersionAsync(
                coupon,
                It.Is<byte[]>(v => v != null && v.Length == 4 && v[0] == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // Version is database-generated, so there is no setter to call in a unit test.
        private static void SetVersion(Coupon coupon, byte[] version)
        {
            typeof(Coupon).GetProperty(nameof(Coupon.Version)).SetValue(coupon, version);
        }

        private void ConfigureRule(string name, CouponRuleCategory category, ICouponRuleParameterValidator validator)
        {
            _registry.Setup(x => x.TryGetRule(name, out It.Ref<CouponRuleDescriptor>.IsAny))
                .Returns((string _, out CouponRuleDescriptor descriptor) =>
                {
                    descriptor = new CouponRuleDescriptor(name, category, null, null, validator, null);
                    return true;
                });
        }

        private void SetupCoupon(Coupon coupon)
        {
            _coupons.Setup(x => x.GetByIdAsync(coupon.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);
            _coupons.Setup(x => x.UpdateWithVersionAsync(coupon, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            SetupTransaction();
        }

        private void SetupTransaction()
        {
            var transaction = new Mock<IOutboxTransaction>();
            transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        }

        private ICouponService CreateService()
            => new CouponService(_coupons.Object, _couponUsed.Object, _moduleClient.Object, _unitOfWork.Object, _outboxWriter.Object, _scopeTargets.Object, _pipeline.Object, new CouponsOptions(), _records.Object, _registry.Object);

        private static Coupon CreateCoupon()
        {
            var coupon = Coupon.CreateWithRules(
                "SAVE10",
                "desc",
                CouponRuleDefinition.SerializeList(new[]
                {
                    new CouponRuleDefinition(CouponRuleNames.OrderTotal, CouponRuleCategory.Scope, new Dictionary<string, string>()),
                    new CouponRuleDefinition(CouponRuleNames.PercentageOff, CouponRuleCategory.Discount, new Dictionary<string, string> { ["percent"] = "10" })
                }),
                new List<CouponScopeTarget>());
            EntityIdSetter.Set(coupon, new CouponId(1));
            return coupon;
        }

        private static UpdateCouponDto FormDto(string percent)
            => new()
            {
                Id = 1,
                Code = "SAVE10",
                Description = "desc",
                Rules = new CouponRulesFormDto
                {
                    Scope = new RuleSelectionDto { Name = CouponRuleNames.OrderTotal },
                    Discount = new RuleSelectionDto
                    {
                        Name = CouponRuleNames.PercentageOff,
                        Parameters = new Dictionary<string, string> { ["percent"] = percent }
                    }
                }
            };
    }
}
