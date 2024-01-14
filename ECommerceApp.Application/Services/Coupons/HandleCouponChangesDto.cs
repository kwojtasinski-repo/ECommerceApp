using ECommerceApp.Application.DTO;

namespace ECommerceApp.Application.Services.Coupons
{
    public class HandleCouponChangesDto
    {
        private readonly int? _couponUsedId;
        private readonly string _promoCode;

        private HandleCouponChangesDto() { }

        public HandleCouponChangesDto(int couponUsedId) : this(couponUsedId, null) { }

        public HandleCouponChangesDto(string couponCode) : this(null, couponCode) { }

        public HandleCouponChangesDto(int? couponUsedId, string couponCode)
        {
            _couponUsedId = couponUsedId;
            _promoCode = couponCode;
        }

        public HandleCouponChangesDto(UpdateOrderDto dto)
        {
            _couponUsedId = dto.CouponUsedId;
            _promoCode = dto.PromoCode;
        }

        public bool HasAnyCoupon()
        {
            return _couponUsedId.HasValue;
        }

        public bool HasNewCouponCode()
        {
            return !string.IsNullOrEmpty(_promoCode);
        }

        public int? CouponUsedId => _couponUsedId;

        public string PromoCode => _promoCode;

        public static HandleCouponChangesDto Of() 
            => new ();

        public static HandleCouponChangesDto Of(int couponUsedId)
            => new (couponUsedId);

        public static HandleCouponChangesDto Of(string couponCode)
            => new (couponCode);

        public static HandleCouponChangesDto Of(int? couponUsedId, string promoCode)
            => Of(couponUsedId, promoCode);
    }
}
