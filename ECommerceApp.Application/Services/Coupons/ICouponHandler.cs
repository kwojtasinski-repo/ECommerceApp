using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Domain.Model;

namespace ECommerceApp.Application.Services.Coupons
{
    internal interface ICouponHandler
    {
        void HandleCouponChangesOnOrder(CouponVm coupon, Order order, HandleCouponChangesDto dto);
    }
}
