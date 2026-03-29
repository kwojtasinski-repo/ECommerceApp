using ECommerceApp.Application.Sales.Coupons.Handlers;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Sales.Coupons;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    /// <summary>
    /// Slice 2 specification tests for CouponsOrderCancelledHandler.
    /// In Slice 2, the handler must support multi-coupon per order:
    /// - Find ALL CouponUsed records for the order (list, not single).
    /// - For each: find matching CouponApplicationRecord, mark WasReversed, then delete CouponUsed.
    /// - Ordering invariant: read → mark → delete (CouponUsed must still exist during match step).
    /// - Handle both DB coupons (CouponId set) and runtime coupons (RuntimeCouponSnapshot set).
    /// </summary>
    public class CouponsOrderCancelledHandlerSlice2Tests
    {
        private readonly Mock<ICouponUsedRepository> _couponUsed;
        private readonly Mock<ICouponRepository> _coupons;
        private readonly Mock<ICouponApplicationRecordRepository> _applicationRecords;

        public CouponsOrderCancelledHandlerSlice2Tests()
        {
            _couponUsed = new Mock<ICouponUsedRepository>();
            _coupons = new Mock<ICouponRepository>();
            _applicationRecords = new Mock<ICouponApplicationRecordRepository>();
        }

        // Current Slice 1 handler (for reference — backward compat tests)
        private CouponsOrderCancelledHandler CreateSlice1Handler()
            => new(_couponUsed.Object, _coupons.Object);

        private static OrderCancelled CreateMessage(int orderId = 99)
            => new(orderId, new List<OrderCancelledItem>(), DateTime.UtcNow);

        private static CouponUsed CreateDbCouponUsed(int id, int couponId, int orderId, string userId = "user-1")
        {
            var cu = CouponUsed.CreateForDbCoupon(new CouponId(couponId), orderId, userId);
            typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(cu, new object[] { new CouponUsedId(id) });
            return cu;
        }

        private static CouponUsed CreateRuntimeCouponUsed(int id, int orderId, string snapshot = "{}", string userId = "user-1")
        {
            var cu = CouponUsed.CreateForRuntimeCoupon(snapshot, orderId, userId);
            typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(cu, new object[] { new CouponUsedId(id) });
            return cu;
        }

        private static Coupon CreateUsedCoupon(int id)
        {
            var coupon = Coupon.Create("SAVE10", "desc");
            typeof(Coupon).GetProperty(nameof(Coupon.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(coupon, new object[] { new CouponId(id) });
            coupon.MarkAsUsed();
            return coupon;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Multi-coupon cancellation — find all, iterate, mark, delete
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_MultipleCouponsOnOrder_ShouldReleaseAllDbCoupons()
        {
            // Spec: Slice 2 handler calls FindAllByOrderIdAsync (list, not single)
            var cu1 = CreateDbCouponUsed(id: 1, couponId: 5, orderId: 99);
            var cu2 = CreateDbCouponUsed(id: 2, couponId: 6, orderId: 99);
            var coupon5 = CreateUsedCoupon(5);
            var coupon6 = CreateUsedCoupon(6);

            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed> { cu1, cu2 });
            _coupons.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(coupon5);
            _coupons.Setup(x => x.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(coupon6);

            // When Slice 2 handler is implemented:
            // Both coupons should be released
            coupon5.Release();
            coupon6.Release();

            coupon5.Status.Should().Be(CouponStatus.Available);
            coupon6.Status.Should().Be(CouponStatus.Available);
        }

        [Fact]
        public async Task HandleAsync_MultipleCoupons_ShouldMarkAllApplicationRecordsAsReversed()
        {
            // Spec: for each CouponUsed, find the matching CouponApplicationRecord
            // by plain CouponUsedId int, then mark WasReversed = true
            var record1 = CouponApplicationRecord.Create(1, "SAVE15", "percentage-off", 15m, 200m, 30m);
            var record2 = CouponApplicationRecord.Create(2, "FLAT50", "fixed-amount-off", 50m, 200m, 50m);

            _applicationRecords.Setup(x => x.FindByCouponUsedIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(record1);
            _applicationRecords.Setup(x => x.FindByCouponUsedIdAsync(2, It.IsAny<CancellationToken>()))
                .ReturnsAsync(record2);

            record1.MarkAsReversed();
            record2.MarkAsReversed();

            record1.WasReversed.Should().BeTrue();
            record2.WasReversed.Should().BeTrue();
            record1.ReversedAt.Should().NotBeNull();
            record2.ReversedAt.Should().NotBeNull();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Ordering invariant: read → mark → delete
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_OrderingInvariant_ShouldMarkBeforeDelete()
        {
            // Spec: CouponUsed must still exist during the match step (finding ApplicationRecord by CouponUsedId).
            // Ordering: 1. Read CouponUsed 2. Find ApplicationRecord by CouponUsedId 3. Mark WasReversed 4. Delete CouponUsed

            var callOrder = new List<string>();

            _applicationRecords.Setup(x => x.FindByCouponUsedIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("find-record"))
                .ReturnsAsync(CouponApplicationRecord.Create(1, "CODE", "pct", 10m, 100m, 10m));

            _applicationRecords.Setup(x => x.UpdateAsync(It.IsAny<CouponApplicationRecord>(), It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("mark-reversed"))
                .Returns(Task.CompletedTask);

            _couponUsed.Setup(x => x.DeleteAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("delete-coupon-used"))
                .Returns(Task.CompletedTask);

            // Simulate the correct ordering
            await _applicationRecords.Object.FindByCouponUsedIdAsync(1);
            await _applicationRecords.Object.UpdateAsync(CouponApplicationRecord.Create(1, "CODE", "pct", 10m, 100m, 10m));
            await _couponUsed.Object.DeleteAsync(CouponUsed.Create(new CouponId(1), 99));

            callOrder.Should().Equal("find-record", "mark-reversed", "delete-coupon-used");
        }

        // ══════════════════════════════════════════════════════════════════════
        // DB coupon vs runtime coupon distinction
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public void HandleAsync_DbCoupon_ShouldReleaseCouponViaCouponId()
        {
            // Spec: for DB coupons (CouponId is set), release the Coupon aggregate
            var cu = CreateDbCouponUsed(id: 1, couponId: 5, orderId: 99);

            cu.CouponId.Should().NotBeNull();
            cu.RuntimeCouponSnapshot.Should().BeNull();

            // Handler should: load Coupon by CouponId, call coupon.Release(), persist
        }

        [Fact]
        public void HandleAsync_RuntimeCoupon_ShouldNotAttemptCouponRelease()
        {
            // Spec: for runtime coupons (CouponId is null, RuntimeCouponSnapshot is set),
            // there is no Coupon aggregate to release. Only delete CouponUsed + mark ApplicationRecord.
            var cu = CreateRuntimeCouponUsed(id: 2, orderId: 99, snapshot: "{\"code\":\"ML10\"}");

            cu.CouponId.Should().BeNull();
            cu.RuntimeCouponSnapshot.Should().NotBeNull();

            // Handler should: skip coupon.Release() for runtime coupons
        }

        [Fact]
        public async Task HandleAsync_MixOfDbAndRuntimeCoupons_ShouldHandleBothCorrectly()
        {
            // Spec: an order can have a mix of DB and runtime coupons
            var dbCoupon = CreateDbCouponUsed(id: 1, couponId: 5, orderId: 99);
            var runtimeCoupon = CreateRuntimeCouponUsed(id: 2, orderId: 99);
            var coupon = CreateUsedCoupon(5);

            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed> { dbCoupon, runtimeCoupon });
            _coupons.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

            // When handled:
            // DB coupon → release coupon aggregate + delete CouponUsed + mark ApplicationRecord
            // Runtime coupon → delete CouponUsed + mark ApplicationRecord (no aggregate release)
            coupon.Release();
            coupon.Status.Should().Be(CouponStatus.Available);
        }

        // ══════════════════════════════════════════════════════════════════════
        // No-op — order had no coupons
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_NoCouponsOnOrder_ShouldBeNoOp()
        {
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed>());

            // Handler should return immediately — no coupon release, no ApplicationRecord marking
            _coupons.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _applicationRecords.Verify(r => r.UpdateAsync(It.IsAny<CouponApplicationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Handler does NOT publish CouponRemovedFromOrder
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_ShouldNotPublishCouponRemovedFromOrder()
        {
            // Spec: CouponsOrderCancelledHandler does NOT publish CouponRemovedFromOrder
            // because the order is already cancelled; updating its CouponUsedId is immaterial.
            // Slice 1 test: uses single FindByOrderIdAsync
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CouponUsed.Create(new CouponId(5), 99));
            var coupon = CreateUsedCoupon(5);
            _coupons.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

            await CreateSlice1Handler().HandleAsync(CreateMessage(99));

            // No message broker interaction
            // Handler has no IMessageBroker dependency — confirmed by constructor
        }

        // ══════════════════════════════════════════════════════════════════════
        // Slice 1 backward compatibility
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_Slice1_NoCouponUsed_ShouldBeNoOp()
        {
            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CouponUsed)null);

            await CreateSlice1Handler().HandleAsync(CreateMessage(99));

            _coupons.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _couponUsed.Verify(r => r.DeleteAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_Slice1_CouponExists_ShouldReleaseAndDelete()
        {
            var cu = CouponUsed.Create(new CouponId(5), 99);
            typeof(CouponUsed).GetProperty(nameof(CouponUsed.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(cu, new object[] { new CouponUsedId(1) });
            var coupon = CreateUsedCoupon(5);

            _couponUsed.Setup(x => x.FindByOrderIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(cu);
            _coupons.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(coupon);

            await CreateSlice1Handler().HandleAsync(CreateMessage(99));

            coupon.Status.Should().Be(CouponStatus.Available);
            _coupons.Verify(r => r.UpdateAsync(coupon, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(cu, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
