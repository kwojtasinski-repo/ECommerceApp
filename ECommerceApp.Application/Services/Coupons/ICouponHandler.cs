using ECommerceApp.Domain.Model;

namespace ECommerceApp.Application.Services.Coupons
{
    internal interface ICouponHandler
    {
        void HandleCouponChangesOnOrder(Order order, HandleCouponChangesDto dto);
    }
}
