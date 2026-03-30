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
        private readonly CouponsOptions _options;
        private readonly ICouponApplicationRecordRepository _applicationRecords;

        public CouponService(
            ICouponRepository coupons,
            ICouponUsedRepository couponUsed,
            IOrderExistenceChecker orderExistence,
            IMessageBroker broker,
            IScopeTargetRepository scopeTargets,
            ICouponRulePipeline pipeline,
            CouponsOptions options,
            ICouponApplicationRecordRepository applicationRecords)
        {
            _coupons = coupons;
            _couponUsed = couponUsed;
            _orderExistence = orderExistence;
            _broker = broker;
            _scopeTargets = scopeTargets;
            _pipeline = pipeline;
            _options = options;
            _applicationRecords = applicationRecords;
        }

        public async Task<CouponApplyResult> ApplyCouponAsync(string couponCode, CouponEvaluationContext context, CancellationToken ct = default)
        {
            if (!await _orderExistence.ExistsAsync(context.OrderId, ct))
            {
                return CouponApplyResult.OrderNotFound;
            }

            var coupon = await _coupons.GetByCodeAsync(couponCode, ct);
            if (coupon is null)
            {
                return CouponApplyResult.CouponNotFound;
            }

            if (coupon.Status != CouponStatus.Available)
            {
                return CouponApplyResult.CouponAlreadyUsed;
            }

            var existingCoupons = await _couponUsed.FindAllByOrderIdAsync(context.OrderId, ct);
            var maxCoupons = Math.Min(_options.MaxCouponsPerOrder, 10);
            if (existingCoupons.Count >= maxCoupons)
            {
                return CouponApplyResult.OrderAlreadyHasCoupon;
            }

            var previousRecords = existingCoupons.Count > 0
                ? await _applicationRecords.FindByOrderIdAsync(context.OrderId, ct)
                : Array.Empty<CouponApplicationRecord>();
            var totalPreviousReductions = previousRecords.Sum(r => r.Reduction);
            var effectivePrice = context.OriginalTotal - totalPreviousReductions;
            if (effectivePrice <= 0m)
            {
                return CouponApplyResult.NoDiscountProduced;
            }

            var rules = coupon.GetRules();
            var discountRule = rules.FirstOrDefault(r => r.Category == CouponRuleCategory.Discount);
            var discountValue = ExtractDiscountValue(discountRule);

            if (discountRule?.Parameters.ContainsKey("amount") == true && discountValue > context.OriginalTotal)
            {
                return CouponApplyResult.RulesNotSatisfied;
            }

            CouponRulePipelineResult pipelineResult = null;
            if (rules.Count > 0)
            {
                pipelineResult = await _pipeline.EvaluateAsync(rules, context, ct);
                if (!pipelineResult.Passed)
                {
                    return CouponApplyResult.RulesNotSatisfied;
                }
            }

            var intendedReduction = pipelineResult?.TotalReduction ?? 0m;
            var actualReduction = Math.Min(intendedReduction, effectivePrice);
            if (actualReduction <= 0m)
            {
                return CouponApplyResult.NoDiscountProduced;
            }

            coupon.MarkAsUsed();
            var couponUsed = CouponUsed.CreateForDbCoupon(coupon.Id, context.OrderId, context.UserId);
            await _couponUsed.AddAsync(couponUsed, ct);
            await _coupons.UpdateAsync(coupon, ct);

            var discountType = discountRule?.Name ?? "none";
            var record = CouponApplicationRecord.Create(
                couponUsed.Id.Value, coupon.Code.Value, discountType,
                discountValue, context.OriginalTotal, actualReduction);
            await _applicationRecords.AddAsync(record, ct);

            await _broker.PublishAsync(
                new CouponApplied(context.OrderId, couponUsed.Id.Value, 0),
                new OrderPriceAdjusted(context.OrderId, effectivePrice - actualReduction, -actualReduction, "coupon", couponUsed.Id.Value));

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

        private static decimal ExtractDiscountValue(CouponRuleDefinition discountRule)
        {
            if (discountRule is null)
            { 
                return 0m;
            }

            if (discountRule.Parameters.TryGetValue("percent", out var pct) && decimal.TryParse(pct, out var p))
            {
                return p;
            }

            if (discountRule.Parameters.TryGetValue("amount", out var amt) && decimal.TryParse(amt, out var a))
            {
                return a;
            }

            return 0m;
        }
    }
}
