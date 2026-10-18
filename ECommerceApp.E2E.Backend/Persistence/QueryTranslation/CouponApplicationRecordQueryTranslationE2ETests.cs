using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.E2E.Backend.Infrastructure;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.E2E.Backend.Persistence.QueryTranslation
{
    /// <summary>
    /// Real <see cref="ICouponApplicationRecordRepository"/> queries against a real SQL Server (Testcontainers).
    /// <c>CouponUsed.Id</c> is a <c>TypedId</c> mapped with <c>HasConversion</c>, so a <c>Join</c> on
    /// <c>used.Id.Value</c> against the plain <c>int</c> <c>CouponApplicationRecord.CouponUsedId</c> passes on InMemory
    /// but cannot be translated to SQL. See <see cref="UserProfileQueryTranslationE2ETests"/> for the rationale.
    /// </summary>
    [Collection(MsSqlE2EFixture.CollectionName)]
    public class CouponApplicationRecordQueryTranslationE2ETests : SqlServerE2ETestBase<ICouponApplicationRecordRepository>
    {
        public CouponApplicationRecordQueryTranslationE2ETests(MsSqlE2EFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task FindByOrderIdAsync_OrderWithCouponUsage_ReturnsOnlyThatOrdersRecords()
        {
            var orderId = NewOrderId();
            var otherOrderId = NewOrderId();
            var used = await SeedCouponUsedAsync(orderId);
            var otherUsed = await SeedCouponUsedAsync(otherOrderId);
            await Service.AddAsync(NewRecord(used.Id.Value), CancellationToken);
            await Service.AddAsync(NewRecord(used.Id.Value), CancellationToken);
            await Service.AddAsync(NewRecord(otherUsed.Id.Value), CancellationToken);

            var found = await Service.FindByOrderIdAsync(orderId, CancellationToken);

            found.Count.ShouldBe(2);
            found.Select(r => r.CouponUsedId).Distinct().ShouldBe(new[] { used.Id.Value });
        }

        [Fact]
        public async Task FindByOrderIdAsync_OrderWithoutCouponUsage_ReturnsEmptyList()
        {
            var found = await Service.FindByOrderIdAsync(NewOrderId(), CancellationToken);

            found.ShouldBeEmpty();
        }

        // Unique per test: the database is shared by the whole collection.
        private static int NewOrderId() => TestIds.Next();

        private async Task<CouponUsed> SeedCouponUsedAsync(int orderId)
        {
            // CouponUsed.CouponId is required and unique, so each usage needs its own coupon.
            var coupon = Coupon.Create($"CAR-{Guid.NewGuid():N}", "translation");
            await GetRequiredService<ICouponRepository>().AddAsync(coupon, CancellationToken);
            var used = CouponUsed.CreateForDbCoupon(coupon.Id, orderId, $"user-{Guid.NewGuid():N}");
            await GetRequiredService<ICouponUsedRepository>().AddAsync(used, CancellationToken);
            return used;
        }

        private static CouponApplicationRecord NewRecord(int couponUsedId)
            => CouponApplicationRecord.Create(couponUsedId, "CODE", "Percentage", 10m, 100m, 10m);
    }
}
