using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
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
        private readonly IModuleClient _moduleClient;
        private readonly IOutboxWriter _outboxWriter;
        private readonly ICouponsUnitOfWork _unitOfWork;
        private readonly IScopeTargetRepository _scopeTargets;
        private readonly ICouponRulePipeline _pipeline;
        private readonly CouponsOptions _options;
        private readonly ICouponApplicationRecordRepository _applicationRecords;
        private readonly ICouponRuleRegistry _ruleRegistry;

        public CouponService(
            ICouponRepository coupons,
            ICouponUsedRepository couponUsed,
            IModuleClient moduleClient,
            ICouponsUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            IScopeTargetRepository scopeTargets,
            ICouponRulePipeline pipeline,
            CouponsOptions options,
            ICouponApplicationRecordRepository applicationRecords,
            ICouponRuleRegistry ruleRegistry)
        {
            _coupons = coupons;
            _couponUsed = couponUsed;
            _moduleClient = moduleClient;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
            _scopeTargets = scopeTargets;
            _pipeline = pipeline;
            _options = options;
            _applicationRecords = applicationRecords;
            _ruleRegistry = ruleRegistry;
        }

        public async Task<CouponApplyResult> ApplyCouponAsync(string couponCode, CouponEvaluationContext context, CancellationToken ct = default)
        {
            if (!await _moduleClient.SendAsync(new OrderExistsQuery(context.OrderId), ct))
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

            var transaction = await _unitOfWork.BeginTransactionAsync(CancellationToken.None);
            await using (transaction)
            {
                await _couponUsed.AddAsync(couponUsed, ct);
                await _coupons.UpdateAsync(coupon, ct);

                var discountType = discountRule?.Name ?? "none";
                var record = CouponApplicationRecord.Create(
                    couponUsed.Id.Value, coupon.Code.Value, discountType,
                    discountValue, context.OriginalTotal, actualReduction);
                await _applicationRecords.AddAsync(record, ct);

                await _outboxWriter.EnqueueAsync(new CouponApplied(context.OrderId, couponUsed.Id.Value, 0), transaction, CancellationToken.None);
                await _outboxWriter.EnqueueAsync(new OrderPriceAdjusted(context.OrderId, effectivePrice - actualReduction, -actualReduction, "coupon", couponUsed.Id.Value), transaction, CancellationToken.None);

                await transaction.CommitAsync(CancellationToken.None);
            }

            return CouponApplyResult.Applied;
        }

        public async Task<CouponRemoveResult> RemoveCouponAsync(int orderId, CancellationToken ct = default)
        {
            var couponUsed = await _couponUsed.FindByOrderIdAsync(orderId, ct);
            if (couponUsed is null)
                return CouponRemoveResult.NoCouponApplied;

            var coupon = await _coupons.GetByIdAsync(couponUsed.CouponId.Value, ct);
            coupon.Release();

            var transaction = await _unitOfWork.BeginTransactionAsync(CancellationToken.None);
            await using (transaction)
            {
                await _couponUsed.DeleteAsync(couponUsed, ct);
                await _coupons.UpdateAsync(coupon, ct);
                await _outboxWriter.EnqueueAsync(new CouponRemovedFromOrder(orderId), transaction, CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
            }
            return CouponRemoveResult.Removed;
        }

        public async Task<CouponApplicationResult> CreateCouponAsync(CreateCouponDto dto, CancellationToken ct = default)
        {
            var existing = await _coupons.GetByCodeAsync(dto.Code, ct);
            if (existing is not null)
            {
                return CouponApplicationResult.Failed($"Coupon code '{dto.Code}' already exists.");
            }

            // Only the mapping is guarded — it is the one step that rejects client input by throwing.
            // Persistence stays outside, so an EF InvalidOperationException surfaces as a real failure
            // instead of being reported to the admin as a coupon validation message.
            List<CouponRuleDefinition> rules;
            try
            {
                rules = MapRules(dto.Rules);
            }
            catch (UnknownCouponRuleException ex)
            {
                return CouponApplicationResult.Failed(ex.Message);
            }

            var validationErrors = new List<string>();
            foreach (var rule in rules)
            {
                if (_ruleRegistry.TryGetRule(rule.Name, out var descriptor) && descriptor.ParameterValidator != null)
                {
                    validationErrors.AddRange(descriptor.ParameterValidator.Validate(rule.Parameters));
                }
            }

            if (validationErrors.Count > 0)
            {
                return CouponApplicationResult.Failed(string.Join(" ", validationErrors));
            }

            var rulesJson = CouponRuleDefinition.SerializeList(rules);
            var scopeTargetsForValidation = (dto.ScopeTargets ?? new List<ScopeTargetDto>())
                .Select(t => CouponScopeTarget.Create(new CouponId(0), t.ScopeType, t.TargetId, t.TargetName))
                .ToList();

            Coupon coupon;
            try
            {
                coupon = Coupon.CreateWithRules(dto.Code, dto.Description, rulesJson, scopeTargetsForValidation);
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

        private List<CouponRuleDefinition> MapRules(CouponRulesFormDto form)
        {
            var selections = new List<(RuleSelectionDto Selection, CouponRuleCategory Category)>
            {
                (form?.Scope, CouponRuleCategory.Scope),
                (form?.Discount, CouponRuleCategory.Discount)
            };

            if (form?.Constraints != null)
            {
                selections.AddRange(form.Constraints.Select(selection => (selection, CouponRuleCategory.Constraint)));
            }

            var rules = new List<CouponRuleDefinition>();
            foreach (var item in selections)
            {
                if (item.Selection == null)
                {
                    continue;
                }

                if (!_ruleRegistry.TryGetRule(item.Selection.Name, out var descriptor))
                {
                    throw new UnknownCouponRuleException(item.Selection.Name);
                }

                rules.Add(new CouponRuleDefinition(
                    descriptor.Name,
                    descriptor.Category,
                    item.Selection.Parameters ?? new Dictionary<string, string>()));
            }

            return rules;
        }

        public CouponRuleCatalogVm GetRuleCatalog()
        {
            var categories = new[]
            {
                CouponRuleCategory.Scope,
                CouponRuleCategory.Discount,
                CouponRuleCategory.Constraint
            };

            return new CouponRuleCatalogVm
            {
                Categories = categories.Select(category => new CouponRuleCatalogCategoryVm
                {
                    Category = category,
                    Rules = _ruleRegistry.GetRulesByCategory(category)
                        .Where(descriptor => descriptor.Name != CouponRuleNames.OversizeGuard)
                        .Select(descriptor => new CouponRuleCatalogRuleVm
                        {
                            Name = descriptor.Name,
                            RequiresScopeTargets = category == CouponRuleCategory.Scope && descriptor.Name != CouponRuleNames.OrderTotal,
                            Parameters = descriptor.ParameterSchema?.GetParameters() ?? new List<CouponRuleParameter>()
                        }).ToList()
                }).ToList()
            };
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

        public async Task<CouponDetailVm> GetCouponAsync(int id, CancellationToken ct = default)
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
