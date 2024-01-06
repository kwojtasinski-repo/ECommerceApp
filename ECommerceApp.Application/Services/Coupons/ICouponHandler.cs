using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Domain.Model;

namespace ECommerceApp.Application.Services.Coupons
{
    internal interface ICouponHandler
    {
        void HandleCouponChangesOnUpdateOrder(CouponVm coupon, Order order, DTO.UpdateOrderDto dto);
    }
}
