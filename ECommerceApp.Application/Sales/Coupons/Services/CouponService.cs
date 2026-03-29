using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.DTOs;
using ECommerceApp.Application.Sales.Coupons.Messages;
using ECommerceApp.Application.Sales.Coupons.Results;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Application.Sales.Coupons.ViewModels;
using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Application.Sales.Coupons.Services
{
    internal sealed class CouponService : ICouponService
    {
        private readonly ICouponRepository _coupons;
        private readonly ICouponUsedRepository _couponUsed;
        private readonly IOrderExistenceChecker _orderExistence;
        private readonly IMessageBroker _broker;
        private readonly IScopeTargetRepository _scopeTargets;
        private readonly ICouponRulePipeline _pipeline;

        public CouponService(
            ICouponRepository coupons,
            ICouponUsedRepository couponUsed,
            IOrderExistenceChecker orderExistence,
            IMessageBroker broker,
            IScopeTargetRepository scopeTargets,
            ICouponRulePipeline pipeline)
        {
            _coupons = coupons;
            _couponUsed = couponUsed;
            _orderExistence = orderExistence;
            _broker = broker;
            _scopeTargets = scopeTargets;
            _pipeline = pipeline;
        }

        public async Task<CouponApplyResult> ApplyCouponAsync(string couponCode, CouponEvaluationContext context, CancellationToken ct = default)
        {
            if (!await _orderExistence.ExistsAsync(context.OrderId, ct))
                return CouponApplyResult.OrderNotFound;

            var coupon = await _coupons.GetByCodeAsync(couponCode, ct);
            if (coupon is null)
                return CouponApplyResult.CouponNotFound;

            if (coupon.Status != CouponStatus.Available)
                return CouponApplyResult.CouponAlreadyUsed;

            var existing = await _couponUsed.FindByOrderIdAsync(context.OrderId, ct);
            if (existing is not null)
                return CouponApplyResult.OrderAlreadyHasCoupon;

            var rules = coupon.GetRules();
            if (rules.Count > 0)
            {
                var pipelineResult = await _pipeline.EvaluateAsync(rules, context, ct);
                if (!pipelineResult.Passed)
                    return CouponApplyResult.RulesNotSatisfied;
            }

            coupon.MarkAsUsed();
            var couponUsed = CouponUsed.Create(coupon.Id, context.OrderId);
            await _couponUsed.AddAsync(couponUsed, ct);
            await _coupons.UpdateAsync(coupon, ct);
            await _broker.PublishAsync(new CouponApplied(context.OrderId, couponUsed.Id.Value, 0));
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

        public async Task<CouponApplicationResult> CreateCouponAsync(CreateCouponDto dto, CancellationToken ct = default)
        {
            var existing = await _coupons.GetByCodeAsync(dto.Code, ct);
            if (existing is not null)
                return CouponApplicationResult.Failed($"Coupon code '{dto.Code}' already exists.");

            var scopeTargetsForValidation = (dto.ScopeTargets ?? new List<ScopeTargetDto>())
                .Select(t => CouponScopeTarget.Create(new CouponId(0), t.ScopeType, t.TargetId, t.TargetName))
                .ToList();

            Coupon coupon;
            try
            {
                coupon = Coupon.CreateWithRules(dto.Code, dto.Description, dto.RulesJson, scopeTargetsForValidation);
            }
            catch (DomainException ex)
            {
                return CouponApplicationResult.Failed(ex.Message);
            }

            await _coupons.AddAsync(coupon, ct);

            if (dto.ScopeTargets is { Count: > 0 })
            {
                var targets = dto.ScopeTargets
                    .Select(t => CouponScopeTarget.Create(coupon.Id, t.ScopeType, t.TargetId, t.TargetName))
                    .ToList();
                await _scopeTargets.AddRangeAsync(targets, ct);
            }

            return CouponApplicationResult.Applied();
        }

        public async Task<bool> AddCouponAsync(string code, string description, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(description))
            {
                return false;
            }

            var existing = await _coupons.GetByCodeAsync(code, ct);
            if (existing is not null)
            {
                return false;
            }

            var coupon = Coupon.Create(code, description);
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
                    Code = c.Code.Value,
                    Description = c.Description.Value,
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
            {
                return null;
            }

            return new CouponDetailVm
            {
                Id = coupon.Id.Value,
                Code = coupon.Code.Value,
                Description = coupon.Description.Value,
                Status = coupon.Status.ToString(),
                RulesJson = coupon.RulesJson
            };
        }

        public async Task<bool> UpdateCouponAsync(UpdateCouponDto dto, CancellationToken ct = default)
        {
            var coupon = await _coupons.GetByIdAsync(dto.Id, ct);
            if (coupon is null)
            {
                return false;
            }

            coupon.Update(dto.Code, dto.Description);
            await _coupons.UpdateAsync(coupon, ct);
            return true;
        }

        public async Task<bool> DeleteCouponAsync(int id, CancellationToken ct = default)
        {
            var coupon = await _coupons.GetByIdAsync(id, ct);
            if (coupon is null)
            {
                return false;
            }

            await _coupons.DeleteAsync(coupon, ct);
            return true;
        }

        public async Task<CouponRulePipelineResult> SimulateCouponAsync(string couponCode, CouponEvaluationContext context, CancellationToken ct = default)
        {
            var coupon = await _coupons.GetByCodeAsync(couponCode, ct);
            if (coupon is null)
            {
                return CouponRulePipelineResult.Failure(new[] { $"Coupon '{couponCode}' not found." });
            }

            if (coupon.Status != CouponStatus.Available)
            {
                return CouponRulePipelineResult.Failure(new[] { $"Coupon '{couponCode}' is not available." });
            }

            var rules = coupon.GetRules();
            if (rules.Count == 0)
            {
                return CouponRulePipelineResult.Success(0m);
            }

            return await _pipeline.EvaluateAsync(rules, context, ct);
        }
    }
}
