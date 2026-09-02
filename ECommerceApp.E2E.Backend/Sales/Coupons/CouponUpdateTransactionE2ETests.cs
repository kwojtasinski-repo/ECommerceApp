using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.E2E.Backend.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ECommerceApp.E2E.Backend.Sales.Coupons
{
    [Collection(MsSqlE2EFixture.CollectionName)]
    public class CouponUpdateTransactionE2ETests : SqlServerE2ETestBase<ICouponService>
    {
        private readonly MsSqlE2EFixture _fixture;

        public CouponUpdateTransactionE2ETests(MsSqlE2EFixture fixture) : base(fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task UpdateCouponAsync_OrderTotalToPerProduct_PersistsRulesAndTarget()
        {
            var coupon = await CreateCouponAsync(CouponRuleNames.OrderTotal);
            var result = await UpdateAsync(coupon, CouponRuleNames.PerProduct, 101, "Product 101");

            result.Outcome.ShouldBe(CouponUpdateOutcome.Updated);
            var state = await ReadStateAsync(coupon.Id.Value);
            state.Coupon.GetRules().Single(rule => rule.Category == CouponRuleCategory.Scope)
                .Name.ShouldBe(CouponRuleNames.PerProduct);
            state.Targets.ShouldContain(target => target.TargetId == 101);
        }

        [Fact]
        public async Task UpdateCouponAsync_PerProductToOrderTotal_RemovesTargets()
        {
            var coupon = await CreateCouponAsync(CouponRuleNames.PerProduct, 201, "Product 201");
            var result = await UpdateAsync(coupon, CouponRuleNames.OrderTotal);

            result.Outcome.ShouldBe(CouponUpdateOutcome.Updated);
            (await ReadStateAsync(coupon.Id.Value)).Targets.ShouldBeEmpty();
        }

        [Fact]
        public async Task UpdateCouponAsync_PerProductTargetChange_ReplacesExistingTarget()
        {
            var coupon = await CreateCouponAsync(CouponRuleNames.PerProduct, 301, "Product 301");
            var result = await UpdateAsync(coupon, CouponRuleNames.PerProduct, 302, "Product 302");

            result.Outcome.ShouldBe(CouponUpdateOutcome.Updated);
            var targets = (await ReadStateAsync(coupon.Id.Value)).Targets;
            targets.Count.ShouldBe(1);
            targets[0].TargetId.ShouldBe(302);
        }

        [Fact]
        public async Task UpdateCouponAsync_OverLongTargetName_FailsValidationAndWritesNothing()
        {
            // The nvarchar(200) TargetName column used to be the only schema constraint the domain
            // did not mirror, so an over-long name reached SaveChanges and surfaced as an unhandled
            // DbUpdateException. CouponScopeTarget.Create now guards it, and CouponService builds the
            // targets inside its DomainException handler — this asserts both halves, on a real
            // database, and that nothing was written on the way out.
            var coupon = await CreateCouponAsync(CouponRuleNames.PerProduct, 401, "Product 401");
            var originalRulesJson = coupon.RulesJson;

            var result = await UpdateAsync(coupon, CouponRuleNames.PerProduct, 402, new string('N', 250));

            result.Outcome.ShouldBe(CouponUpdateOutcome.ValidationFailed);
            result.FailureReason.ShouldContain("200");

            var state = await ReadStateAsync(coupon.Id.Value);
            state.Coupon.RulesJson.ShouldBe(originalRulesJson);
            state.Targets.Count.ShouldBe(1);
            state.Targets.Single().TargetId.ShouldBe(401);
        }

        private async Task<Coupon> CreateCouponAsync(string scope, int? targetId = null, string targetName = null)
        {
            var code = $"TX-{Guid.NewGuid():N}";
            var coupon = Coupon.CreateWithRules(
                code,
                "transaction",
                CreateRulesJson(scope, targetId),
                targetId.HasValue
                    ? new[] { CouponScopeTarget.Create(new CouponId(0), scope, targetId.Value, targetName) }
                    : Array.Empty<CouponScopeTarget>());
            await GetRequiredService<ICouponRepository>().AddAsync(coupon, CancellationToken);

            if (targetId.HasValue)
            {
                await GetRequiredService<IScopeTargetRepository>().AddRangeAsync(
                    new[] { CouponScopeTarget.Create(coupon.Id, scope, targetId.Value, targetName) },
                    CancellationToken);
            }

            return coupon;
        }

        private async Task<CouponUpdateResult> UpdateAsync(Coupon coupon, string scope, int? targetId = null, string targetName = null)
        {
            using var scopeLifetime = _fixture.Services.CreateScope();
            var service = scopeLifetime.ServiceProvider.GetRequiredService<ICouponService>();
            var current = await scopeLifetime.ServiceProvider.GetRequiredService<ICouponRepository>()
                .GetByIdAsync(coupon.Id.Value, CancellationToken);
            return await service.UpdateCouponAsync(new UpdateCouponDto
            {
                Id = current.Id.Value,
                Code = current.Code.Value,
                Description = current.Description.Value,
                Version = current.Version,
                Rules = new CouponRulesFormDto
                {
                    Scope = new RuleSelectionDto
                    {
                        Name = scope,
                        Parameters = scope == CouponRuleNames.PerProduct
                            ? new Dictionary<string, string> { ["productId"] = targetId?.ToString() ?? "1" }
                            : new Dictionary<string, string>()
                    },
                    Discount = new RuleSelectionDto
                    {
                        Name = CouponRuleNames.PercentageOff,
                        Parameters = new Dictionary<string, string> { ["percent"] = "10" }
                    }
                },
                ScopeTargets = targetId.HasValue
                    ? new List<ScopeTargetDto> { new() { ScopeType = scope, TargetId = targetId.Value, TargetName = targetName } }
                    : new List<ScopeTargetDto>()
            }, CancellationToken);
        }

        private async Task<(Coupon Coupon, IReadOnlyList<CouponScopeTarget> Targets)> ReadStateAsync(int couponId)
        {
            using var scope = _fixture.Services.CreateScope();
            var provider = scope.ServiceProvider;
            return (
                await provider.GetRequiredService<ICouponRepository>().GetByIdAsync(couponId, CancellationToken),
                await provider.GetRequiredService<IScopeTargetRepository>().GetByCouponIdAsync(couponId, CancellationToken));
        }

        private static string CreateRulesJson(string scope, int? targetId)
            => CouponRuleDefinition.SerializeList(new[]
            {
                new CouponRuleDefinition(scope, CouponRuleCategory.Scope, scope == CouponRuleNames.PerProduct
                    ? new Dictionary<string, string> { ["productId"] = targetId?.ToString() ?? "1" }
                    : new Dictionary<string, string>()),
                new CouponRuleDefinition(CouponRuleNames.PercentageOff, CouponRuleCategory.Discount,
                    new Dictionary<string, string> { ["percent"] = "10" })
            });
    }
}