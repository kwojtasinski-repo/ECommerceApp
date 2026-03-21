using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Domain.Sales.Coupons;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponRuleRegistryTests
    {
        // ── helpers ───────────────────────────────────────────────────────────

        private static CouponWorkflowBuilder CreateBuilder() => new();

        private static ICouponRuleEvaluator StubEvaluator(string name)
            => new StubSyncEvaluator(name);

        // ── Build — valid registry ────────────────────────────────────────────

        [Fact]
        public void Build_WithRulesRegistered_ShouldReturnImmutableRegistry()
        {
            var builder = CreateBuilder()
                .DefineRule("order-total", CouponRuleCategory.Scope, StubEvaluator("order-total"))
                .DefineRule("percentage-off", CouponRuleCategory.Discount, StubEvaluator("percentage-off"))
                .DefineRule("max-uses", CouponRuleCategory.Constraint, StubEvaluator("max-uses"));

            var registry = builder.Build();

            registry.GetAllRules().Should().HaveCount(3);
        }

        [Fact]
        public void Build_RegistryShouldBeImmutable_SubsequentDefineRuleShouldNotAffect()
        {
            var builder = CreateBuilder()
                .DefineRule("order-total", CouponRuleCategory.Scope, StubEvaluator("order-total"))
                .DefineRule("percentage-off", CouponRuleCategory.Discount, StubEvaluator("percentage-off"));

            var registry = builder.Build();
            builder.DefineRule("extra", CouponRuleCategory.Constraint, StubEvaluator("extra"));

            registry.GetAllRules().Should().HaveCount(2);
        }

        // ── GetRule ───────────────────────────────────────────────────────────

        [Fact]
        public void GetRule_ExistingRule_ShouldReturnDescriptor()
        {
            var builder = CreateBuilder()
                .DefineRule("percentage-off", CouponRuleCategory.Discount, StubEvaluator("percentage-off"));
            var registry = builder.Build();

            var descriptor = registry.GetRule("percentage-off");

            descriptor.Should().NotBeNull();
            descriptor.Name.Should().Be("percentage-off");
            descriptor.Category.Should().Be(CouponRuleCategory.Discount);
        }

        [Fact]
        public void GetRule_NonExistentRule_ShouldThrowOrReturnNull()
        {
            var builder = CreateBuilder()
                .DefineRule("percentage-off", CouponRuleCategory.Discount, StubEvaluator("percentage-off"));
            var registry = builder.Build();

            var act = () => registry.GetRule("does-not-exist");

            act.Should().Throw<KeyNotFoundException>();
        }

        // ── TryGetRule ────────────────────────────────────────────────────────

        [Fact]
        public void TryGetRule_ExistingRule_ShouldReturnTrueAndDescriptor()
        {
            var builder = CreateBuilder()
                .DefineRule("order-total", CouponRuleCategory.Scope, StubEvaluator("order-total"));
            var registry = builder.Build();

            var found = registry.TryGetRule("order-total", out var descriptor);

            found.Should().BeTrue();
            descriptor.Should().NotBeNull();
            descriptor.Name.Should().Be("order-total");
        }

        [Fact]
        public void TryGetRule_NonExistentRule_ShouldReturnFalse()
        {
            var builder = CreateBuilder()
                .DefineRule("order-total", CouponRuleCategory.Scope, StubEvaluator("order-total"));
            var registry = builder.Build();

            var found = registry.TryGetRule("no-such-rule", out var descriptor);

            found.Should().BeFalse();
            descriptor.Should().BeNull();
        }

        // ── GetRulesByCategory ────────────────────────────────────────────────

        [Fact]
        public void GetRulesByCategory_ShouldFilterCorrectly()
        {
            var builder = CreateBuilder()
                .DefineRule("order-total", CouponRuleCategory.Scope, StubEvaluator("order-total"))
                .DefineRule("per-product", CouponRuleCategory.Scope, StubEvaluator("per-product"))
                .DefineRule("percentage-off", CouponRuleCategory.Discount, StubEvaluator("percentage-off"))
                .DefineRule("max-uses", CouponRuleCategory.Constraint, StubEvaluator("max-uses"))
                .DefineRule("valid-date-range", CouponRuleCategory.Constraint, StubEvaluator("valid-date-range"));
            var registry = builder.Build();

            registry.GetRulesByCategory(CouponRuleCategory.Scope).Should().HaveCount(2);
            registry.GetRulesByCategory(CouponRuleCategory.Discount).Should().HaveCount(1);
            registry.GetRulesByCategory(CouponRuleCategory.Constraint).Should().HaveCount(2);
        }

        // ── DefineRule — validation ───────────────────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void DefineRule_EmptyName_ShouldThrow(string name)
        {
            var builder = CreateBuilder();

            var act = () => builder.DefineRule(name, CouponRuleCategory.Scope, StubEvaluator("x"));

            act.Should().Throw<System.ArgumentException>().WithMessage("*name*required*");
        }

        // ── Full initial vocabulary registration ──────────────────────────────

        [Fact]
        public void Build_AllInitialRuleVocabulary_ShouldRegisterAllRules()
        {
            var builder = CreateBuilder()
                // Scope rules
                .DefineRule("order-total", CouponRuleCategory.Scope, StubEvaluator("order-total"))
                .DefineRule("per-product", CouponRuleCategory.Scope, StubEvaluator("per-product"))
                .DefineRule("per-category", CouponRuleCategory.Scope, StubEvaluator("per-category"))
                .DefineRule("per-tag", CouponRuleCategory.Scope, StubEvaluator("per-tag"))
                // Discount rules
                .DefineRule("percentage-off", CouponRuleCategory.Discount, StubEvaluator("percentage-off"))
                .DefineRule("fixed-amount-off", CouponRuleCategory.Discount, StubEvaluator("fixed-amount-off"))
                .DefineRule("free-item", CouponRuleCategory.Discount, StubEvaluator("free-item"))
                .DefineRule("gift-product", CouponRuleCategory.Discount, StubEvaluator("gift-product"))
                .DefineRule("free-cheapest-item", CouponRuleCategory.Discount, StubEvaluator("free-cheapest-item"))
                // Constraint rules
                .DefineRule("max-uses", CouponRuleCategory.Constraint, StubEvaluator("max-uses"))
                .DefineRule("max-uses-per-user", CouponRuleCategory.Constraint, StubEvaluator("max-uses-per-user"))
                .DefineRule("valid-date-range", CouponRuleCategory.Constraint, StubEvaluator("valid-date-range"))
                .DefineRule("min-order-value", CouponRuleCategory.Constraint, StubEvaluator("min-order-value"))
                .DefineRule("special-event", CouponRuleCategory.Constraint, StubEvaluator("special-event"))
                .DefineRule("first-purchase-only", CouponRuleCategory.Constraint, StubEvaluator("first-purchase-only"));

            var registry = builder.Build();

            registry.GetAllRules().Should().HaveCount(16);  // 15 explicit + 1 auto-injected oversize guard
            registry.GetRulesByCategory(CouponRuleCategory.Scope).Should().HaveCount(4);
            registry.GetRulesByCategory(CouponRuleCategory.Discount).Should().HaveCount(5);
            registry.GetRulesByCategory(CouponRuleCategory.Constraint).Should().HaveCount(7);  // 6 explicit + oversize guard
        }

        // ── Stub evaluator for testing ────────────────────────────────────────

        private sealed class StubSyncEvaluator : ICouponRuleEvaluator
        {
            public string RuleName { get; }

            public StubSyncEvaluator(string ruleName) => RuleName = ruleName;

            public CouponRuleEvaluationResult Evaluate(CouponEvaluationContext context, IReadOnlyDictionary<string, string> parameters)
                => CouponRuleEvaluationResult.Pass();
        }
    }
}
