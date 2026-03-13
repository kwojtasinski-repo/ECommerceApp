using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public class Coupon
    {
        public CouponId Id { get; private set; }
        public string Code { get; private set; }
        public int DiscountPercent { get; private set; }
        public string Description { get; private set; }
        public CouponStatus Status { get; private set; }

        private Coupon() { }

        public static Coupon Create(string code, int discountPercent, string description)
            => new Coupon
            {
                Code = code,
                DiscountPercent = discountPercent,
                Description = description,
                Status = CouponStatus.Available
            };

        public void MarkAsUsed()
        {
            if (Status != CouponStatus.Available)
                throw new DomainException($"Coupon '{Code}' is not available.");
            Status = CouponStatus.Used;
        }

        public void Release()
        {
            if (Status != CouponStatus.Used)
                throw new DomainException($"Coupon '{Code}' is not in Used status.");
            Status = CouponStatus.Available;
        }
    }
}
