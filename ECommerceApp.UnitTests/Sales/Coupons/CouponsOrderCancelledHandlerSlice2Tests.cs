using ECommerceApp.Application.Sales.Coupons.Handlers;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Sales.Coupons;
using AwesomeAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private CouponsOrderCancelledHandler CreateHandler()
            => new(_couponUsed.Object, _coupons.Object, _applicationRecords.Object);

        private static OrderCancelled CreateMessage(int orderId = 99)
            => new(orderId, new List<OrderCancelledItem>(), DateTime.UtcNow);

        private static CouponUsed CreateDbCouponUsed(int id, int couponId, int orderId, string userId = "user-1")
        {
            var cu = CouponUsed.CreateForDbCoupon(new CouponId(couponId), orderId, userId);
            EntityIdSetter.Set(cu, new CouponUsedId(id));
            return cu;
        }

        private static CouponUsed CreateRuntimeCouponUsed(int id, int orderId, string snapshot = "{}", string userId = "user-1")
        {
            var cu = CouponUsed.CreateForRuntimeCoupon(snapshot, orderId, userId);
            EntityIdSetter.Set(cu, new CouponUsedId(id));
            return cu;
        }

        private static Coupon CreateUsedCoupon(int id)
        {
            var coupon = Coupon.Create("SAVE10", "desc");
            EntityIdSetter.Set(coupon, new CouponId(id));
            coupon.MarkAsUsed();
            return coupon;
        }

        private void SetupDbCoupons(params (CouponUsed Used, Coupon Coupon)[] entries)
        {
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries.Select(entry => entry.Used).ToList());
            foreach (var entry in entries)
            {
                _coupons.Setup(x => x.GetByIdAsync(entry.Used.CouponId!.Value, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(entry.Coupon);
            }
        }

        private void SetupSingleCoupon(CouponUsed couponUsed, Coupon coupon)
        {
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed> { couponUsed });
            if (couponUsed.CouponId is not null)
            {
                _coupons.Setup(x => x.GetByIdAsync(couponUsed.CouponId.Value, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(coupon);
            }
        }

        private void SetupApplicationRecordLookup(int couponUsedId, CouponApplicationRecord record)
        {
            _applicationRecords.Setup(x => x.FindByCouponUsedIdAsync(couponUsedId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(record);
        }

        private void SetupOrderedApplicationRecordLookup(List<string> callOrder, CouponApplicationRecord record)
        {
            _applicationRecords.Setup(x => x.FindByCouponUsedIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("find-record"))
                .ReturnsAsync(record);
        }

        private void SetupOrderedApplicationRecordUpdate(List<string> callOrder)
        {
            _applicationRecords.Setup(x => x.UpdateAsync(It.IsAny<CouponApplicationRecord>(), It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("mark-reversed"))
                .Returns(Task.CompletedTask);
        }

        private void SetupOrderedCouponUsedDeletion(List<string> callOrder)
        {
            _couponUsed.Setup(x => x.DeleteAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("delete-coupon-used"))
                .Returns(Task.CompletedTask);
        }

        private void SetupNoCouponsForOrder()
        {
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed>());
        }

        private void SetupMixedCouponTypes(CouponUsed dbCoupon, CouponUsed runtimeCoupon, Coupon coupon)
        {
            _couponUsed.Setup(x => x.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CouponUsed> { dbCoupon, runtimeCoupon });
            _coupons.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(coupon);
        }

        private void SetupSlice1Coupon(CouponUsed couponUsed, Coupon coupon)
        {
            SetupSingleCoupon(couponUsed, coupon);
        }

        private void SetupCouponUsedWithoutApplicationRecord(CouponUsed couponUsed, Coupon coupon)
        {
            SetupSingleCoupon(couponUsed, coupon);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Multi-coupon cancellation — find all, iterate, mark, delete
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_MultipleCouponsOnOrder_ShouldReleaseAllDbCoupons()
        {
            // Arrange
            var cu1 = CreateDbCouponUsed(id: 1, couponId: 5, orderId: 99);
            var cu2 = CreateDbCouponUsed(id: 2, couponId: 6, orderId: 99);
            var coupon5 = CreateUsedCoupon(5);
            var coupon6 = CreateUsedCoupon(6);

            SetupDbCoupons((cu1, coupon5), (cu2, coupon6));

            // Act
            await CreateHandler().HandleAsync(CreateMessage(99), TestContext.Current.CancellationToken);

            // Assert
            coupon5.Status.Should().Be(CouponStatus.Available);
            coupon6.Status.Should().Be(CouponStatus.Available);
            _coupons.Verify(r => r.UpdateAsync(coupon5, It.IsAny<CancellationToken>()), Times.Once);
            _coupons.Verify(r => r.UpdateAsync(coupon6, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(cu1, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(cu2, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_MultipleCoupons_ShouldMarkAllApplicationRecordsAsReversed()
        {
            // Arrange
            var cu1 = CreateDbCouponUsed(id: 1, couponId: 5, orderId: 99);
            var cu2 = CreateDbCouponUsed(id: 2, couponId: 6, orderId: 99);
            var coupon5 = CreateUsedCoupon(5);
            var coupon6 = CreateUsedCoupon(6);
            var record1 = CouponApplicationRecord.Create(1, "SAVE15", "percentage-off", 15m, 200m, 30m);
            var record2 = CouponApplicationRecord.Create(2, "FLAT50", "fixed-amount-off", 50m, 200m, 50m);

            SetupDbCoupons((cu1, coupon5), (cu2, coupon6));
            SetupApplicationRecordLookup(1, record1);
            SetupApplicationRecordLookup(2, record2);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(99), TestContext.Current.CancellationToken);

            // Assert
            record1.WasReversed.Should().BeTrue();
            record2.WasReversed.Should().BeTrue();
            record1.ReversedAt.Should().NotBeNull();
            record2.ReversedAt.Should().NotBeNull();
            _applicationRecords.Verify(r => r.UpdateAsync(record1, It.IsAny<CancellationToken>()), Times.Once);
            _applicationRecords.Verify(r => r.UpdateAsync(record2, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Ordering invariant: read → mark → delete
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_OrderingInvariant_ShouldMarkBeforeDelete()
        {
            // Arrange
            var cu = CreateDbCouponUsed(id: 1, couponId: 5, orderId: 99);
            var coupon = CreateUsedCoupon(5);
            var callOrder = new List<string>();

            SetupSingleCoupon(cu, coupon);
            SetupOrderedApplicationRecordLookup(
                callOrder,
                CouponApplicationRecord.Create(1, "CODE", "pct", 10m, 100m, 10m));
            SetupOrderedApplicationRecordUpdate(callOrder);
            SetupOrderedCouponUsedDeletion(callOrder);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(99), TestContext.Current.CancellationToken);

            // Assert
            callOrder.Should().Equal("find-record", "mark-reversed", "delete-coupon-used");
        }

        // ══════════════════════════════════════════════════════════════════════
        // DB coupon vs runtime coupon distinction
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_DbCoupon_ShouldReleaseCouponViaCouponId()
        {
            // Arrange
            var cu = CreateDbCouponUsed(id: 1, couponId: 5, orderId: 99);
            var coupon = CreateUsedCoupon(5);
            SetupSingleCoupon(cu, coupon);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(99), TestContext.Current.CancellationToken);

            // Assert
            coupon.Status.Should().Be(CouponStatus.Available);
            _coupons.Verify(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()), Times.Once);
            _coupons.Verify(r => r.UpdateAsync(coupon, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(cu, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_RuntimeCoupon_ShouldNotAttemptCouponRelease()
        {
            // Arrange
            var cu = CreateRuntimeCouponUsed(id: 2, orderId: 99, snapshot: "{\"code\":\"ML10\"}");
            var record = CouponApplicationRecord.Create(2, "ML10", "percentage-off", 10m, 100m, 10m);
            SetupCouponUsedWithoutApplicationRecord(cu, null);
            SetupApplicationRecordLookup(2, record);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(99), TestContext.Current.CancellationToken);

            // Assert
            record.WasReversed.Should().BeTrue();
            _coupons.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _coupons.Verify(r => r.UpdateAsync(It.IsAny<Coupon>(), It.IsAny<CancellationToken>()), Times.Never);
            _applicationRecords.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(cu, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_MixOfDbAndRuntimeCoupons_ShouldHandleBothCorrectly()
        {
            // Arrange
            var dbCoupon = CreateDbCouponUsed(id: 1, couponId: 5, orderId: 99);
            var runtimeCoupon = CreateRuntimeCouponUsed(id: 2, orderId: 99);
            var coupon = CreateUsedCoupon(5);

            SetupMixedCouponTypes(dbCoupon, runtimeCoupon, coupon);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(99), TestContext.Current.CancellationToken);

            // Assert
            coupon.Status.Should().Be(CouponStatus.Available);
            _coupons.Verify(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()), Times.Once);
            _coupons.Verify(r => r.UpdateAsync(coupon, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(dbCoupon, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(runtimeCoupon, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ══════════════════════════════════════════════════════════════════════
        // No-op — order had no coupons
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_NoCouponsOnOrder_ShouldBeNoOp()
        {
            // Arrange
            SetupNoCouponsForOrder();

            // Act
            await CreateHandler().HandleAsync(CreateMessage(99), TestContext.Current.CancellationToken);

            // Assert
            _coupons.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _applicationRecords.Verify(r => r.UpdateAsync(It.IsAny<CouponApplicationRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Handler does NOT publish CouponRemovedFromOrder
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_ShouldNotPublishCouponRemovedFromOrder()
        {
            // Arrange
            var cu = CreateDbCouponUsed(id: 1, couponId: 5, orderId: 99);
            var coupon = CreateUsedCoupon(5);
            SetupSingleCoupon(cu, coupon);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(99), TestContext.Current.CancellationToken);

            // Assert
            coupon.Status.Should().Be(CouponStatus.Available);
            _couponUsed.Verify(r => r.FindAllByOrderIdAsync(99, It.IsAny<CancellationToken>()), Times.Once);
            _coupons.Verify(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()), Times.Once);
            _coupons.Verify(r => r.UpdateAsync(coupon, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(cu, It.IsAny<CancellationToken>()), Times.Once);
            _applicationRecords.Verify(r => r.FindByCouponUsedIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.VerifyNoOtherCalls();
            _coupons.VerifyNoOtherCalls();
            _applicationRecords.VerifyNoOtherCalls();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Slice 1 backward compatibility
        // ══════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task HandleAsync_Slice1_NoCouponUsed_ShouldBeNoOp()
        {
            // Arrange
            SetupNoCouponsForOrder();

            // Act
            await CreateHandler().HandleAsync(CreateMessage(99), TestContext.Current.CancellationToken);

            // Assert
            _coupons.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _couponUsed.Verify(r => r.DeleteAsync(It.IsAny<CouponUsed>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_Slice1_CouponExists_ShouldReleaseAndDelete()
        {
            // Arrange
            var cu = CouponUsed.Create(new CouponId(5), 99);
            EntityIdSetter.Set(cu, new CouponUsedId(1));
            var coupon = CreateUsedCoupon(5);

            SetupSlice1Coupon(cu, coupon);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(99), TestContext.Current.CancellationToken);

            // Assert
            coupon.Status.Should().Be(CouponStatus.Available);
            _coupons.Verify(r => r.UpdateAsync(coupon, It.IsAny<CancellationToken>()), Times.Once);
            _couponUsed.Verify(r => r.DeleteAsync(cu, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
