using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;

namespace ECommerceApp.Application.Services.Coupons
{
    internal class CouponHandler : ICouponHandler
    {
        private readonly ICouponRepository _couponRepository;
        private readonly ICouponUsedRepository _couponUsedRepository;

        public CouponHandler(ICouponRepository couponRepository, ICouponUsedRepository couponUsedRepository)
        {
            _couponRepository = couponRepository;
            _couponUsedRepository = couponUsedRepository;
        }

        public void HandleCouponChangesOnUpdateOrder(CouponVm couponVm, Order order, DTO.UpdateOrderDto dto)
        {
            if (order is null)
            {
                throw new BusinessException($"{nameof(Order)} cannot be null");
            }

            if (dto is null)
            {
                throw new BusinessException($"{nameof(DTO.UpdateOrderDto)} cannot be null");
            }

            if (couponVm is null && dto.CouponUsedId is null && order.CouponUsedId is null)
            {
                return;
            }

            if (dto.CouponUsedId == order.CouponUsedId && string.IsNullOrEmpty(dto.PromoCode))
            {
                return;
            }

            if (couponVm is null && dto.CouponUsedId is null && order.CouponUsedId is not null)
            {
                foreach (var orderItem in order.OrderItems)
                {
                    orderItem.CouponUsed = null;
                    orderItem.CouponUsedId = null;
                }
                order.CouponUsed = null;
                order.CouponUsedId = null;
                return;
            }

            var coupon = _couponRepository.GetById(couponVm.Id)
                ?? throw new BusinessException($"Coupon with id '{couponVm.Id}' was not found");

            if (coupon.CouponUsedId.HasValue)
            {
                throw new BusinessException("Cannot assign used coupon");
            }

            if (order.CouponUsed is not null || order.CouponUsedId.HasValue)
            {
                if (!_couponUsedRepository.Delete(order.CouponUsedId.Value))
                {
                    throw new BusinessException($"Cannot delete coupon used with id '{order.CouponUsedId}'");
                }
            }

            var couponUsed = new CouponUsed
            {
                Id = couponVm.Id,
                CouponId = couponVm.Id,
                OrderId = order.Id,
                Coupon = coupon,
                Order = order,
            };
            _couponUsedRepository.Add(couponUsed);
            coupon.CouponUsed = couponUsed;
            coupon.CouponUsedId = couponUsed.Id;
            _couponRepository.Update(coupon);
            order.CouponUsed = couponUsed;
            order.CouponUsedId = couponUsed.Id;

            foreach (var orderItem in order.OrderItems)
            {
                orderItem.CouponUsed = couponUsed;
                orderItem.CouponUsedId = couponUsed.Id;
            }
        }
    }
}
