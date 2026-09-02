using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Results;
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
    public class CouponConcurrencyE2ETests : SqlServerE2ETestBase<ICouponRepository>
    {
        private readonly MsSqlE2EFixture _fixture;

        public CouponConcurrencyE2ETests(MsSqlE2EFixture fixture) : base(fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CouponRepository_AddAsync_SqlServerGeneratesRowVersion()
        {
            var coupon = Coupon.Create($"VERSION-{Guid.NewGuid():N}", "version");

            await Service.AddAsync(coupon, CancellationToken);

            coupon.Version.ShouldNotBeNull();
            coupon.Version.Length.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task CouponService_TwoEditsFromTheSameVersion_SecondReturnsConcurrencyConflict()
        {
            // The case phase 20c could only mock: both edits start from the version the form loaded,
            // the first commits and SQL Server bumps the rowversion, the second is refused. Nothing
            // here is hand-thrown — the conflict comes from a version the database actually changed.
            var code = $"RACE-{Guid.NewGuid():N}";
            await Service.AddAsync(Coupon.Create(code, "original"), CancellationToken);
            var loaded = await Service.GetByCodeAsync(code, CancellationToken);
            var versionTheEditBeganFrom = (byte[])loaded.Version.Clone();

            var firstOutcome = await UpdateThroughServiceAsync(
                loaded.Id.Value, code, "first edit", versionTheEditBeganFrom);
            var secondOutcome = await UpdateThroughServiceAsync(
                loaded.Id.Value, code, "second edit", versionTheEditBeganFrom);

            firstOutcome.Outcome.ShouldBe(CouponUpdateOutcome.Updated);
            secondOutcome.Outcome.ShouldBe(CouponUpdateOutcome.ConcurrencyConflict);

            using var scope = _fixture.Services.CreateScope();
            var stored = await scope.ServiceProvider.GetRequiredService<ICouponRepository>()
                .GetByIdAsync(loaded.Id.Value, CancellationToken);
            stored.Description.Value.ShouldBe("first edit");
        }

        [Fact]
        public async Task CouponRepository_UpdateWithVersionAsync_CompetingWrites_ReturnsConcurrencyException()
        {
            var code = $"CONFLICT-{Guid.NewGuid():N}";
            await Service.AddAsync(Coupon.Create(code, "original"), CancellationToken);

            using var firstScope = _fixture.Services.CreateScope();
            using var secondScope = _fixture.Services.CreateScope();
            var firstRepository = firstScope.ServiceProvider.GetRequiredService<ICouponRepository>();
            var secondRepository = secondScope.ServiceProvider.GetRequiredService<ICouponRepository>();
            var first = await firstRepository.GetByCodeAsync(code, CancellationToken);
            var second = await secondRepository.GetByCodeAsync(code, CancellationToken);
            var originalVersion = (byte[])first.Version.Clone();

            first.Update(code, "first update");
            await firstRepository.UpdateWithVersionAsync(first, originalVersion, CancellationToken);

            second.Update(code, "second update");
            var exception = await Record.ExceptionAsync(
                () => secondRepository.UpdateWithVersionAsync(second, originalVersion, CancellationToken));

            exception.ShouldNotBeNull();
            exception.ShouldBeOfType<DbUpdateConcurrencyException>();
        }

        [Fact]
        public async Task CouponService_UpdateCouponAsync_CurrentVersion_SucceedsAndChangesVersion()
        {
            var code = $"UPDATE-{Guid.NewGuid():N}";
            var coupon = Coupon.Create(code, "original");
            await Service.AddAsync(coupon, CancellationToken);
            var loaded = await Service.GetByCodeAsync(code, CancellationToken);
            var originalVersion = (byte[])loaded.Version.Clone();

            var result = await UpdateThroughServiceAsync(loaded.Id.Value, code, "updated", originalVersion);

            result.Outcome.ShouldBe(CouponUpdateOutcome.Updated);
            using var scope = _fixture.Services.CreateScope();
            var updated = await scope.ServiceProvider.GetRequiredService<ICouponRepository>()
                .GetByIdAsync(loaded.Id.Value, CancellationToken);
            updated.Version.ShouldNotBe(originalVersion);
            updated.Description.Value.ShouldBe("updated");
        }

        private async Task<CouponUpdateResult> UpdateThroughServiceAsync(
            int couponId, string code, string description, byte[] version)
        {
            // A fresh scope per edit: two updates sharing one DbContext would share a change tracker
            // and never model two independent callers.
            using var scope = _fixture.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ICouponService>().UpdateCouponAsync(
                new UpdateCouponDto
                {
                    Id = couponId,
                    Code = code,
                    Description = description,
                    Version = version,
                    Rules = new CouponRulesFormDto
                    {
                        Scope = new RuleSelectionDto { Name = CouponRuleNames.OrderTotal },
                        Discount = new RuleSelectionDto
                        {
                            Name = CouponRuleNames.PercentageOff,
                            Parameters = new Dictionary<string, string> { ["percent"] = "10" }
                        }
                    }
                },
                CancellationToken);
        }
    }
}