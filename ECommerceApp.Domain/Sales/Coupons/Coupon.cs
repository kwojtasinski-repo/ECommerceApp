using ECommerceApp.Domain.Sales.Coupons.ValueObjects;
using ECommerceApp.Domain.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ECommerceApp.Domain.Sales.Coupons
{
    public class Coupon
    {
        public CouponId Id { get; private set; }
        public CouponCode Code { get; private set; }
        public CouponDescription Description { get; private set; }
        public CouponStatus Status { get; private set; }

        // ── Slice 2 additions ────────────────────────────────────────────
        public string RulesJson { get; private set; }      // serialized CouponRuleDefinition[]
        public byte[] Version { get; private set; }        // rowversion for optimistic concurrency
        public bool BypassOversizeGuard { get; private set; }  // admin override for the oversize guard (default: false)

        private Coupon() { }

        // Slice 1 factory — kept for backward compatibility
        public static Coupon Create(string code, string description)
            => new Coupon
            {
                Code = new CouponCode(code),
                Description = new CouponDescription(description),
                Status = CouponStatus.Available
            };

        // Slice 2 factory — rule-based coupon creation
        public static Coupon CreateWithRules(string code, string description, string rulesJson, IReadOnlyList<CouponScopeTarget> scopeTargets)
        {
            if (string.IsNullOrWhiteSpace(rulesJson))
            {
                throw new DomainException("RulesJson is required.");
            }

            var rules = DeserializeRules(rulesJson);
            ValidateRuleComposition(rules, scopeTargets);

            return new Coupon
            {
                Code = new CouponCode(code),
                Description = new CouponDescription(description),
                RulesJson = rulesJson
            };
        }

        public IReadOnlyList<CouponRuleDefinition> GetRules()
        {
            if (string.IsNullOrWhiteSpace(RulesJson))
            {
                return new List<CouponRuleDefinition>();
            }

            return DeserializeRules(RulesJson);
        }

        public void MarkAsUsed()
        {
            if (Status != CouponStatus.Available)
            {
                throw new DomainException($"Coupon '{Code}' is not available.");
            }

            Status = CouponStatus.Used;
        }

        public void Release()
        {
            if (Status != CouponStatus.Used)
            {
                throw new DomainException($"Coupon '{Code}' is not in Used status.");
            }

            Status = CouponStatus.Available;
        }

        public void Update(string code, string description)
        {
            Code = new CouponCode(code);
            Description = new CouponDescription(description);
        }

        public void UpdateRules(string rulesJson, IReadOnlyList<CouponScopeTarget> scopeTargets)
        {
            if (string.IsNullOrWhiteSpace(rulesJson))
            {
                throw new DomainException("RulesJson is required.");
            }

            var rules = DeserializeRules(rulesJson);
            ValidateRuleComposition(rules, scopeTargets);

            RulesJson = rulesJson;
        }

        // ── Slice 2 rule validation ─────────────────────────────────────

        private static List<CouponRuleDefinition> DeserializeRules(string rulesJson)
        {
            try
            {
                return JsonSerializer.Deserialize<List<CouponRuleDefinition>>(rulesJson)
                       ?? new List<CouponRuleDefinition>();
            }
            catch (JsonException)
            {
                throw new DomainException("RulesJson is not valid JSON.");
            }
        }

        private static void ValidateRuleComposition(IReadOnlyList<CouponRuleDefinition> rules, IReadOnlyList<CouponScopeTarget> scopeTargets)
        {
            var scopeRules = rules.Where(r => r.Category == CouponRuleCategory.Scope).ToList();
            var discountRules = rules.Where(r => r.Category == CouponRuleCategory.Discount).ToList();

            if (scopeRules.Count != 1)
            {
                throw new DomainException($"Exactly one Scope rule is required. Found {scopeRules.Count}.");
            }

            if (discountRules.Count != 1)
            {
                throw new DomainException($"Exactly one Discount rule is required. Found {discountRules.Count}.");
            }

            var scopeRule = scopeRules[0];
            var requiresTargets = scopeRule.Name != CouponRuleNames.OrderTotal;

            if (requiresTargets && (scopeTargets == null || scopeTargets.Count == 0))
            {
                throw new DomainException($"Scope rule '{scopeRule.Name}' requires scope targets.");
            }

            if (!requiresTargets && scopeTargets != null && scopeTargets.Count > 0)
            {
                throw new DomainException($"Scope rule '{CouponRuleNames.OrderTotal}' must not have scope targets.");
            }
        }
    }
}
