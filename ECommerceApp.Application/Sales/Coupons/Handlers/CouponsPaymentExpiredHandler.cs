using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Sales.Coupons;

namespace ECommerceApp.Application.Sales.Coupons.Handlers
{
    internal sealed class CouponsPaymentExpiredHandler : IMessageHandler<PaymentExpired>
    {
        private readonly ICouponUsedRepository _couponUsed;
        private readonly ICouponRepository _coupons;
        private readonly ICouponApplicationRecordRepository _applicationRecords;

        public CouponsPaymentExpiredHandler(
            ICouponUsedRepository couponUsed,
            ICouponRepository coupons,
            ICouponApplicationRecordRepository applicationRecords)
        {
            _couponUsed = couponUsed;
            _coupons = coupons;
            _applicationRecords = applicationRecords;
        }

        public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
        {
            var couponsUsed = await _couponUsed.FindAllByOrderIdAsync(message.OrderId, ct);
            if (couponsUsed.Count == 0)
                return;

            foreach (var couponUsed in couponsUsed)
            {
                if (couponUsed.CouponId is not null)
                {
                    var coupon = await _coupons.GetByIdAsync(couponUsed.CouponId.Value, ct);
                    coupon.Release();
                    await _coupons.UpdateAsync(coupon, ct);
                }

                var record = await _applicationRecords.FindByCouponUsedIdAsync(couponUsed.Id.Value, ct);
                if (record is not null)
                {
                    record.MarkAsReversed();
                    await _applicationRecords.UpdateAsync(record, ct);
                }

                await _couponUsed.DeleteAsync(couponUsed, ct);
            }
        }
    }
}
