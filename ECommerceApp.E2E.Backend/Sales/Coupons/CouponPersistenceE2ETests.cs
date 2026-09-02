using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.E2E.Backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ECommerceApp.E2E.Backend.Sales.Coupons
{
    [Collection(MsSqlE2EFixture.CollectionName)]
    public class CouponPersistenceE2ETests : SqlServerE2ETestBase<ICouponService>
    {
        private readonly MsSqlE2EFixture _fixture;

        public CouponPersistenceE2ETests(MsSqlE2EFixture fixture) : base(fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CouponRepository_DuplicateCode_UniqueIndexRejectsSecondRow()
        {
            // Deliberately bypasses CouponService so the assertion lands on IX_Coupons_Code itself.
            // The service's own pre-check would hide the index; this is the guarantee that survives
            // two callers racing between that pre-check and the insert.
            var code = $"DUP-{Guid.NewGuid():N}";
            var repository = GetRequiredService<ICouponRepository>();
            await repository.AddAsync(Coupon.Create(code, "first"), CancellationToken);

            var exception = await Record.ExceptionAsync(
                () => repository.AddAsync(Coupon.Create(code, "second"), CancellationToken));

            exception.ShouldNotBeNull();
            exception.ShouldBeOfType<DbUpdateException>();
        }

        [Fact]
        public async Task CreateCouponAsync_DuplicateCode_ReturnsHandledFailureRatherThanThrowing()
        {
            var code = $"DUPSVC-{Guid.NewGuid():N}";
            var first = await Service.CreateCouponAsync(NewCouponDto(code), CancellationToken);

            var second = await Service.CreateCouponAsync(NewCouponDto(code), CancellationToken);

            first.Success.ShouldBeTrue(first.FailureReason);
            second.Success.ShouldBeFalse();
            second.FailureReason.ShouldContain("already exists");
        }

        [Fact]
        public async Task CreateCouponAsync_CodeLongerThanFiftyCharacters_IsRejectedByCouponCode()
        {
            // Documents the rejecting layer: CouponCode throws before EF is involved, so the
            // nvarchar(50) column is never reached. Kept as the record of that ordering — if the
            // domain guard is ever dropped, this fails here rather than as a truncation error.
            var dto = new CreateCouponDto
            {
                Code = new string('x', 51),
                Description = "too long",
                Rules = CreateOrderTotalRules()
            };

            var result = await Service.CreateCouponAsync(dto, CancellationToken);

            result.Success.ShouldBeFalse();
            result.FailureReason.ShouldContain("50");
        }

        [Fact]
        public async Task CouponRepository_RulesJson_RoundTripsAllCategoriesAndParameters()
        {
            var code = $"JSON-{Guid.NewGuid():N}";
            var rules = new[]
            {
                new CouponRuleDefinition(CouponRuleNames.OrderTotal, CouponRuleCategory.Scope,
                    new Dictionary<string, string>()),
                new CouponRuleDefinition(CouponRuleNames.PercentageOff, CouponRuleCategory.Discount,
                    new Dictionary<string, string> { ["percent"] = "15", ["label"] = "zniżka" }),
                new CouponRuleDefinition(CouponRuleNames.MinOrderValue, CouponRuleCategory.Constraint,
                    new Dictionary<string, string> { ["amount"] = "123.45", ["note"] = "wartość minimalna" })
            };
            var coupon = Coupon.CreateWithRules(
                code,
                "json round trip",
                CouponRuleDefinition.SerializeList(rules),
                Array.Empty<CouponScopeTarget>());

            await GetRequiredService<ICouponRepository>().AddAsync(coupon, CancellationToken);

            using var scope = _fixture.Services.CreateScope();
            var roundTrip = await scope.ServiceProvider.GetRequiredService<ICouponRepository>()
                .GetByCodeAsync(code, CancellationToken);

            roundTrip.ShouldNotBeNull();
            var roundTripRules = roundTrip.GetRules();
            roundTripRules.Select(rule => rule.Name).ShouldBe(rules.Select(rule => rule.Name));
            roundTripRules.Select(rule => rule.Category).ShouldBe(rules.Select(rule => rule.Category));
            roundTripRules[1].Parameters["percent"].ShouldBe("15");
            roundTripRules[1].Parameters["label"].ShouldBe("zniżka");
            roundTripRules[2].Parameters["amount"].ShouldBe("123.45");
            roundTripRules[2].Parameters["note"].ShouldBe("wartość minimalna");
        }

        [Fact]
        public async Task CouponRepository_LegacyNumericCategoryJson_StillParses()
        {
            var code = $"LEGACY-{Guid.NewGuid():N}";
            var legacyJson = "[{\"Name\":\"order-total\",\"Category\":0,\"Parameters\":{}},{\"Name\":\"percentage-off\",\"Category\":1,\"Parameters\":{\"percent\":\"20\"}}]";
            var coupon = Coupon.CreateWithRules(code, "legacy json", legacyJson, Array.Empty<CouponScopeTarget>());

            await GetRequiredService<ICouponRepository>().AddAsync(coupon, CancellationToken);

            using var scope = _fixture.Services.CreateScope();
            var roundTrip = await scope.ServiceProvider.GetRequiredService<ICouponRepository>()
                .GetByCodeAsync(code, CancellationToken);

            roundTrip.ShouldNotBeNull();
            roundTrip.GetRules().Select(rule => rule.Category).ShouldBe(
                new[] { CouponRuleCategory.Scope, CouponRuleCategory.Discount });
        }

        [Fact]
        public async Task CouponRepository_GetAllAsync_SearchAndPaging_ReturnsStableScopedPages()
        {
            // GetAllAsync orders by Code.Value, not by the typed id, so this is not the TypedId/OrderBy
            // trap fixed in 1a11ed59 — CouponRepository never had it. What it does cover on a real
            // provider is the SQL translation: Contains -> LIKE, and Skip/Take -> OFFSET/FETCH, which
            // SQL Server only accepts alongside an ORDER BY.
            var prefix = $"PAGE-{Guid.NewGuid():N}-";
            var codes = new[] { $"{prefix}A", $"{prefix}B", $"{prefix}C" };
            var repository = GetRequiredService<ICouponRepository>();
            foreach (var code in codes)
            {
                await repository.AddAsync(Coupon.Create(code, "paging"), CancellationToken);
            }

            var firstPage = await repository.GetAllAsync(2, 1, prefix, CancellationToken);
            var secondPage = await repository.GetAllAsync(2, 2, prefix, CancellationToken);

            firstPage.Select(coupon => coupon.Code.Value).ShouldBe(new[] { codes[0], codes[1] });
            secondPage.Select(coupon => coupon.Code.Value).ShouldBe(new[] { codes[2] });
        }

        private static CreateCouponDto NewCouponDto(string code)
            => new()
            {
                Code = code,
                Description = "duplicate probe",
                Rules = CreateOrderTotalRules()
            };

        private static CouponRulesFormDto CreateOrderTotalRules()
            => new()
            {
                Scope = new RuleSelectionDto { Name = CouponRuleNames.OrderTotal },
                Discount = new RuleSelectionDto
                {
                    Name = CouponRuleNames.PercentageOff,
                    Parameters = new Dictionary<string, string> { ["percent"] = "10" }
                }
            };
    }
}