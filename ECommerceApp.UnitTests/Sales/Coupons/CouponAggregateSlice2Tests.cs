using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponAggregateSlice2Tests
    {
        // ── helpers ───────────────────────────────────────────────────────────

        private static string BuildRulesJson(params CouponRuleDefinition[] rules)
            => JsonSerializer.Serialize(rules);

        private static CouponRuleDefinition ScopeRule(string name = "order-total")
            => new(name, CouponRuleCategory.Scope, new Dictionary<string, string>());

        private static CouponRuleDefinition DiscountRule(string name = "percentage-off", Dictionary<string, string> parameters = null)
            => new(name, CouponRuleCategory.Discount, parameters ?? new Dictionary<string, string> { ["percent"] = "15" });

        private static CouponRuleDefinition ConstraintRule(string name = "max-uses", Dictionary<string, string> parameters = null)
            => new(name, CouponRuleCategory.Constraint, parameters ?? new Dictionary<string, string> { ["maxUses"] = "100" });

        private static CouponScopeTarget CreateTarget(int couponId = 1, string scopeType = "per-product", int targetId = 42, string targetName = "Widget")
        {
            return CouponScopeTarget.Create(new CouponId(couponId), scopeType, targetId, targetName);
        }

        // ── CreateWithRules — valid compositions ──────────────────────────────

        [Fact]
        public void CreateWithRules_OrderTotalScope_PercentageOff_NoConstraints_ShouldSucceed()
        {
            var rulesJson = BuildRulesJson(ScopeRule("order-total"), DiscountRule("percentage-off"));

            var coupon = Coupon.CreateWithRules("SAVE15", "15% off order", rulesJson, new List<CouponScopeTarget>());

            coupon.Code.Value.Should().Be("SAVE15");
            coupon.Description.Value.Should().Be("15% off order");
            coupon.RulesJson.Should().Be(rulesJson);
        }

        [Fact]
        public void CreateWithRules_OrderTotalScope_FixedAmountOff_WithConstraints_ShouldSucceed()
        {
            var rulesJson = BuildRulesJson(
                ScopeRule("order-total"),
                DiscountRule("fixed-amount-off", new Dictionary<string, string> { ["amount"] = "50" }),
                ConstraintRule("max-uses", new Dictionary<string, string> { ["maxUses"] = "100" }),
                ConstraintRule("valid-date-range", new Dictionary<string, string> { ["validFrom"] = "2026-01-01", ["validTo"] = "2026-12-31" }));

            var coupon = Coupon.CreateWithRules("FLAT50", "50 off order", rulesJson, new List<CouponScopeTarget>());

            coupon.Code.Value.Should().Be("FLAT50");
            coupon.GetRules().Should().HaveCount(4);
        }

        [Fact]
        public void CreateWithRules_PerProductScope_WithTargets_ShouldSucceed()
        {
            var rulesJson = BuildRulesJson(ScopeRule("per-product"), DiscountRule("percentage-off"));
            var targets = new List<CouponScopeTarget> { CreateTarget(scopeType: "per-product", targetId: 42) };

            var coupon = Coupon.CreateWithRules("PROD15", "15% off product", rulesJson, targets);

            coupon.Code.Value.Should().Be("PROD15");
            coupon.RulesJson.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void CreateWithRules_PerCategoryScope_WithTargets_ShouldSucceed()
        {
            var rulesJson = BuildRulesJson(ScopeRule("per-category"), DiscountRule("percentage-off"));
            var targets = new List<CouponScopeTarget> { CreateTarget(scopeType: "per-category", targetId: 5) };

            var coupon = Coupon.CreateWithRules("CAT15", "15% off category", rulesJson, targets);

            coupon.Code.Value.Should().Be("CAT15");
        }

        [Fact]
        public void CreateWithRules_PerTagScope_WithTargets_ShouldSucceed()
        {
            var rulesJson = BuildRulesJson(ScopeRule("per-tag"), DiscountRule("percentage-off"));
            var targets = new List<CouponScopeTarget> { CreateTarget(scopeType: "per-tag", targetId: 3) };

            var coupon = Coupon.CreateWithRules("TAG15", "15% off tag", rulesJson, targets);

            coupon.Code.Value.Should().Be("TAG15");
        }

        // ── CreateWithRules — scope rule count validation ─────────────────────

        [Fact]
        public void CreateWithRules_NoScopeRule_ShouldThrowDomainException()
        {
            var rulesJson = BuildRulesJson(DiscountRule());

            var act = () => Coupon.CreateWithRules("X", "desc", rulesJson, new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*Exactly one Scope rule*Found 0*");
        }

        [Fact]
        public void CreateWithRules_TwoScopeRules_ShouldThrowDomainException()
        {
            var rulesJson = BuildRulesJson(ScopeRule("order-total"), ScopeRule("per-product"), DiscountRule());

            var act = () => Coupon.CreateWithRules("X", "desc", rulesJson, new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*Exactly one Scope rule*Found 2*");
        }

        // ── CreateWithRules — discount rule count validation ──────────────────

        [Fact]
        public void CreateWithRules_NoDiscountRule_ShouldThrowDomainException()
        {
            var rulesJson = BuildRulesJson(ScopeRule());

            var act = () => Coupon.CreateWithRules("X", "desc", rulesJson, new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*Exactly one Discount rule*Found 0*");
        }

        [Fact]
        public void CreateWithRules_TwoDiscountRules_ShouldThrowDomainException()
        {
            var rulesJson = BuildRulesJson(ScopeRule(), DiscountRule("percentage-off"), DiscountRule("fixed-amount-off"));

            var act = () => Coupon.CreateWithRules("X", "desc", rulesJson, new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*Exactly one Discount rule*Found 2*");
        }

        // ── CreateWithRules — scope ↔ targets consistency ─────────────────────

        [Fact]
        public void CreateWithRules_PerProductScope_NoTargets_ShouldThrowDomainException()
        {
            var rulesJson = BuildRulesJson(ScopeRule("per-product"), DiscountRule());

            var act = () => Coupon.CreateWithRules("X", "desc", rulesJson, new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*requires scope targets*");
        }

        [Fact]
        public void CreateWithRules_PerCategoryScope_NullTargets_ShouldThrowDomainException()
        {
            var rulesJson = BuildRulesJson(ScopeRule("per-category"), DiscountRule());

            var act = () => Coupon.CreateWithRules("X", "desc", rulesJson, null);

            act.Should().Throw<DomainException>().WithMessage("*requires scope targets*");
        }

        [Fact]
        public void CreateWithRules_OrderTotalScope_WithTargets_ShouldThrowDomainException()
        {
            var rulesJson = BuildRulesJson(ScopeRule("order-total"), DiscountRule());
            var targets = new List<CouponScopeTarget> { CreateTarget() };

            var act = () => Coupon.CreateWithRules("X", "desc", rulesJson, targets);

            act.Should().Throw<DomainException>().WithMessage("*order-total*must not have scope targets*");
        }

        // ── CreateWithRules — multiple constraints allowed ────────────────────

        [Fact]
        public void CreateWithRules_MultipleConstraints_ShouldSucceed()
        {
            var rulesJson = BuildRulesJson(
                ScopeRule("order-total"),
                DiscountRule("percentage-off"),
                ConstraintRule("max-uses"),
                ConstraintRule("max-uses-per-user"),
                ConstraintRule("valid-date-range"),
                ConstraintRule("min-order-value"),
                ConstraintRule("first-purchase-only"));

            var coupon = Coupon.CreateWithRules("FULL", "all constraints", rulesJson, new List<CouponScopeTarget>());

            coupon.GetRules().Should().HaveCount(7);
        }

        [Fact]
        public void CreateWithRules_ZeroConstraints_ShouldSucceed()
        {
            var rulesJson = BuildRulesJson(ScopeRule("order-total"), DiscountRule("percentage-off"));

            var coupon = Coupon.CreateWithRules("NOCONST", "unrestricted", rulesJson, new List<CouponScopeTarget>());

            coupon.GetRules().Should().HaveCount(2);
        }

        // ── CreateWithRules — input validation ────────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void CreateWithRules_EmptyCode_ShouldThrowDomainException(string code)
        {
            var rulesJson = BuildRulesJson(ScopeRule(), DiscountRule());

            var act = () => Coupon.CreateWithRules(code, "desc", rulesJson, new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*code*required*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void CreateWithRules_EmptyDescription_ShouldThrowDomainException(string desc)
        {
            var rulesJson = BuildRulesJson(ScopeRule(), DiscountRule());

            var act = () => Coupon.CreateWithRules("X", desc, rulesJson, new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*Description*required*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void CreateWithRules_EmptyRulesJson_ShouldThrowDomainException(string rulesJson)
        {
            var act = () => Coupon.CreateWithRules("X", "desc", rulesJson, new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*RulesJson*required*");
        }

        [Fact]
        public void CreateWithRules_InvalidJson_ShouldThrowDomainException()
        {
            var act = () => Coupon.CreateWithRules("X", "desc", "not-valid-json{{{", new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*not valid JSON*");
        }

        // ── GetRules ──────────────────────────────────────────────────────────

        [Fact]
        public void GetRules_WithValidRulesJson_ShouldDeserializeCorrectly()
        {
            var rulesJson = BuildRulesJson(ScopeRule("order-total"), DiscountRule("percentage-off", new Dictionary<string, string> { ["percent"] = "20" }));
            var coupon = Coupon.CreateWithRules("RULES", "test", rulesJson, new List<CouponScopeTarget>());

            var rules = coupon.GetRules();

            rules.Should().HaveCount(2);
            rules[0].Name.Should().Be("order-total");
            rules[0].Category.Should().Be(CouponRuleCategory.Scope);
            rules[1].Name.Should().Be("percentage-off");
            rules[1].Category.Should().Be(CouponRuleCategory.Discount);
            rules[1].Parameters["percent"].Should().Be("20");
        }

        [Fact]
        public void GetRules_Slice1CouponWithoutRulesJson_ShouldReturnEmpty()
        {
            var coupon = Coupon.Create("LEGACY", "legacy coupon");

            var rules = coupon.GetRules();

            rules.Should().BeEmpty();
        }

        // ── Discount rule types ───────────────────────────────────────────────

        [Theory]
        [InlineData("percentage-off", "percent", "15")]
        [InlineData("fixed-amount-off", "amount", "50")]
        [InlineData("free-item", "productId", "123")]
        [InlineData("gift-product", "productId", "456")]
        [InlineData("free-cheapest-item", "maxFreeUnits", "1")]
        public void CreateWithRules_EachDiscountType_ShouldSucceed(string discountName, string paramKey, string paramValue)
        {
            var rulesJson = BuildRulesJson(
                ScopeRule("order-total"),
                DiscountRule(discountName, new Dictionary<string, string> { [paramKey] = paramValue }));

            var coupon = Coupon.CreateWithRules("DISC", "disc test", rulesJson, new List<CouponScopeTarget>());

            coupon.GetRules().Should().HaveCount(2);
        }

        // ── Slice 1 backward compat ───────────────────────────────────────────

        [Fact]
        public void Create_Slice1Factory_ShouldStillWork()
        {
            var coupon = Coupon.Create("LEGACY", "legacy");

            coupon.Code.Value.Should().Be("LEGACY");
            coupon.Status.Should().Be(CouponStatus.Available);
            coupon.RulesJson.Should().BeNull();
            coupon.Version.Should().BeNull();
        }

        [Fact]
        public void MarkAsUsed_ShouldStillWorkAfterSlice2Additions()
        {
            var coupon = Coupon.Create("SAVE10", "desc");

            coupon.MarkAsUsed();

            coupon.Status.Should().Be(CouponStatus.Used);
        }

        [Fact]
        public void Release_ShouldStillWorkAfterSlice2Additions()
        {
            var coupon = Coupon.Create("SAVE10", "desc");
            coupon.MarkAsUsed();

            coupon.Release();

            coupon.Status.Should().Be(CouponStatus.Available);
        }

        // ── UpdateRules ───────────────────────────────────────────────────────

        [Fact]
        public void UpdateRules_ValidRulesJson_ShouldReplaceRulesJson()
        {
            var original = BuildRulesJson(ScopeRule("order-total"), DiscountRule("percentage-off"));
            var coupon = Coupon.CreateWithRules("SAVE15", "15% off", original, new List<CouponScopeTarget>());

            var updated = BuildRulesJson(ScopeRule("order-total"), DiscountRule("fixed-amount-off", new Dictionary<string, string> { ["amount"] = "20" }));
            coupon.UpdateRules(updated, new List<CouponScopeTarget>());

            coupon.RulesJson.Should().Be(updated);
            coupon.GetRules().Should().HaveCount(2);
            coupon.GetRules()[1].Name.Should().Be("fixed-amount-off");
        }

        [Fact]
        public void UpdateRules_ChangeScopeFromOrderTotalToPerProduct_ShouldRequireTargets()
        {
            var original = BuildRulesJson(ScopeRule("order-total"), DiscountRule());
            var coupon = Coupon.CreateWithRules("SAVE15", "15% off", original, new List<CouponScopeTarget>());

            var newRules = BuildRulesJson(ScopeRule("per-product"), DiscountRule());
            var act = () => coupon.UpdateRules(newRules, new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*requires scope targets*");
        }

        [Fact]
        public void UpdateRules_ValidPerProductWithTargets_ShouldSucceed()
        {
            var original = BuildRulesJson(ScopeRule("order-total"), DiscountRule());
            var coupon = Coupon.CreateWithRules("SAVE15", "15% off", original, new List<CouponScopeTarget>());
            var target = CreateTarget(couponId: 1, scopeType: "per-product", targetId: 42);

            var newRules = BuildRulesJson(ScopeRule("per-product"), DiscountRule());
            coupon.UpdateRules(newRules, new List<CouponScopeTarget> { target });

            coupon.GetRules()[0].Name.Should().Be("per-product");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void UpdateRules_EmptyRulesJson_ShouldThrowDomainException(string rulesJson)
        {
            var coupon = Coupon.CreateWithRules("X", "desc",
                BuildRulesJson(ScopeRule(), DiscountRule()), new List<CouponScopeTarget>());

            var act = () => coupon.UpdateRules(rulesJson, new List<CouponScopeTarget>());

            act.Should().Throw<DomainException>().WithMessage("*RulesJson*required*");
        }
    }
}
