using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Coupons.ViewModels;
using ECommerceApp.Domain.Sales.Coupons;

namespace ECommerceApp.Application.Sales.Coupons.Services
{
    internal sealed class CouponService : ICouponService
    {
        private readonly ICouponRepository _coupons;
        private readonly ICouponUsedRepository _couponUsed;
        private readonly IOrderExistenceChecker _orderExistence;
        private readonly IMessageBroker _broker;

        public CouponService(
            ICouponRepository coupons,
            ICouponUsedRepository couponUsed,
            IOrderExistenceChecker orderExistence,
            IMessageBroker broker)
        {
            _coupons = coupons;
            _couponUsed = couponUsed;
            _orderExistence = orderExistence;
            _broker = broker;
        }

        public async Task<CouponApplyResult> ApplyCouponAsync(string couponCode, int orderId, CancellationToken ct = default)
        {
            if (!await _orderExistence.ExistsAsync(orderId, ct))
                return CouponApplyResult.OrderNotFound;

            var coupon = await _coupons.GetByCodeAsync(couponCode, ct);
            if (coupon is null)
                return CouponApplyResult.CouponNotFound;

            if (coupon.Status != CouponStatus.Available)
                return CouponApplyResult.CouponAlreadyUsed;

            var existing = await _couponUsed.FindByOrderIdAsync(orderId, ct);
            if (existing is not null)
                return CouponApplyResult.OrderAlreadyHasCoupon;

            coupon.MarkAsUsed();
            var couponUsed = CouponUsed.Create(coupon.Id, orderId);
            await _couponUsed.AddAsync(couponUsed, ct);
            await _coupons.UpdateAsync(coupon, ct);
            await _broker.PublishAsync(new CouponApplied(orderId, couponUsed.Id.Value, coupon.DiscountPercent));
            return CouponApplyResult.Applied;
        }

        public async Task<CouponRemoveResult> RemoveCouponAsync(int orderId, CancellationToken ct = default)
        {
            var couponUsed = await _couponUsed.FindByOrderIdAsync(orderId, ct);
            if (couponUsed is null)
                return CouponRemoveResult.NoCouponApplied;

            var coupon = await _coupons.GetByIdAsync(couponUsed.CouponId.Value, ct);
            coupon.Release();
            await _couponUsed.DeleteAsync(couponUsed, ct);
            await _coupons.UpdateAsync(coupon, ct);
            await _broker.PublishAsync(new CouponRemovedFromOrder(orderId));
            return CouponRemoveResult.Removed;
        }

        public Task<CouponApplicationResult> CreateCouponAsync(CreateCouponDto dto, CancellationToken ct = default)
        {
            throw new NotImplementedException("Slice 2 — rule-based coupon creation");
        }

        public async Task<bool> AddCouponAsync(string code, string description, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(description))
                return false;

            var existing = await _coupons.GetByCodeAsync(code, ct);
            if (existing is not null)
                return false;

            var coupon = Coupon.Create(code, 0, description);
            await _coupons.AddAsync(coupon, ct);
            return true;
        }

        public async Task<CouponListVm> GetCouponsAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var coupons = await _coupons.GetAllAsync(pageSize, pageNo, searchString, ct);
            var total = await _coupons.CountAsync(searchString, ct);
            return new CouponListVm
            {
                Coupons = coupons.Select(c => new CouponForListVm
                {
                    Id = c.Id.Value,
                    Code = c.Code,
                    DiscountPercent = c.DiscountPercent,
                    Description = c.Description,
                    Status = c.Status.ToString()
                }).ToList(),
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = total,
                SearchString = searchString
            };
        }

        public async Task<CouponDetailVm?> GetCouponAsync(int id, CancellationToken ct = default)
        {
            var coupon = await _coupons.GetByIdAsync(id, ct);
            if (coupon is null)
                return null;
            return new CouponDetailVm
            {
                Id = coupon.Id.Value,
                Code = coupon.Code,
                DiscountPercent = coupon.DiscountPercent,
                Description = coupon.Description,
                Status = coupon.Status.ToString(),
                RulesJson = coupon.RulesJson
            };
        }

        public async Task<bool> UpdateCouponAsync(UpdateCouponDto dto, CancellationToken ct = default)
        {
            var coupon = await _coupons.GetByIdAsync(dto.Id, ct);
            if (coupon is null)
                return false;
            coupon.Update(dto.Code, dto.Description);
            await _coupons.UpdateAsync(coupon, ct);
            return true;
        }

        public async Task<bool> DeleteCouponAsync(int id, CancellationToken ct = default)
        {
            var coupon = await _coupons.GetByIdAsync(id, ct);
            if (coupon is null)
                return false;
            await _coupons.DeleteAsync(coupon, ct);
            return true;
        }
    }
}
