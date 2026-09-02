using ECommerceApp.Application.Sales.Coupons;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Application.Sales.Coupons.Rules.Evaluators;
using ECommerceApp.Application.Sales.Coupons.Services;
using ECommerceApp.Domain.Sales.Coupons;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    public class CouponRuleSchemaTests
    {
        [Fact]
        public void RequiredSchemaParameters_ShouldAgreeWithRealValidators()
        {
            var options = new CouponsOptions();
            var validators = new (ICouponRuleParameterValidator Validator, ICouponRuleParameterSchema Schema)[]
            {
                (new OrderTotalScopeEvaluator(), new OrderTotalScopeEvaluator()),
                (new PerProductScopeEvaluator(), new PerProductScopeEvaluator()),
                (new PerCategoryScopeEvaluator(), new PerCategoryScopeEvaluator()),
                (new PerTagScopeEvaluator(), new PerTagScopeEvaluator()),
                (new PercentageOffEvaluator(options), new PercentageOffEvaluator(options)),
                (new FixedAmountOffEvaluator(), new FixedAmountOffEvaluator()),
                (new FreeItemEvaluator(), new FreeItemEvaluator()),
                (new GiftProductEvaluator(), new GiftProductEvaluator()),
                (new FreeCheapestItemEvaluator(), new FreeCheapestItemEvaluator()),
                (new MaxUsesEvaluator(new Mock<ICouponUsedRepository>().Object), new MaxUsesEvaluator(new Mock<ICouponUsedRepository>().Object)),
                (new MaxUsesPerUserEvaluator(new Mock<ICouponUsedRepository>().Object), new MaxUsesPerUserEvaluator(new Mock<ICouponUsedRepository>().Object)),
                (new ValidDateRangeEvaluator(), new ValidDateRangeEvaluator()),
                (new MinOrderValueEvaluator(options), new MinOrderValueEvaluator(options)),
                (new SpecialEventEvaluator(new Mock<ISpecialEventCache>().Object), new SpecialEventEvaluator(new Mock<ISpecialEventCache>().Object)),
                (new FirstPurchaseOnlyEvaluator(new Mock<ICompletedOrderCounter>().Object), new FirstPurchaseOnlyEvaluator(new Mock<ICompletedOrderCounter>().Object))
            };

            foreach (var pair in validators)
            {
                var errors = pair.Validator.Validate(new Dictionary<string, string>());
                foreach (var parameter in pair.Schema.GetParameters())
                {
                    if (parameter.Required)
                    {
                        errors.Should().Contain(error => error.Contains(parameter.Key));
                    }
                    else
                    {
                        errors.Should().NotContain(error => error.Contains(parameter.Key));
                    }
                }
            }
        }

        [Fact]
        public void SchemaKeys_ShouldMatchEachRuleContract()
        {
            new OrderTotalScopeEvaluator().GetParameters().Should().BeEmpty();
            new PerProductScopeEvaluator().GetParameters().Select(p => p.Key).Should().Equal("productId");
            new PerCategoryScopeEvaluator().GetParameters().Select(p => p.Key).Should().Equal("categoryId");
            new PerTagScopeEvaluator().GetParameters().Select(p => p.Key).Should().Equal("tagId");
            new PercentageOffEvaluator(new CouponsOptions()).GetParameters().Select(p => p.Key).Should().Equal("percent");
            new FixedAmountOffEvaluator().GetParameters().Select(p => p.Key).Should().Equal("amount");
            new FreeItemEvaluator().GetParameters().Select(p => p.Key).Should().Equal("productId", "quantity");
            new GiftProductEvaluator().GetParameters().Select(p => p.Key).Should().Equal("productId", "quantity");
            new FreeCheapestItemEvaluator().GetParameters().Select(p => p.Key).Should().Equal("maxFreeUnits");
            new MaxUsesEvaluator(new Mock<ICouponUsedRepository>().Object).GetParameters().Select(p => p.Key).Should().Equal("maxUses");
            new MaxUsesPerUserEvaluator(new Mock<ICouponUsedRepository>().Object).GetParameters().Select(p => p.Key).Should().Equal("maxUsesPerUser");
            new ValidDateRangeEvaluator().GetParameters().Select(p => p.Key).Should().Equal("validFrom", "validTo");
            new MinOrderValueEvaluator(new CouponsOptions()).GetParameters().Select(p => p.Key).Should().Equal("minValue");
            new SpecialEventEvaluator(new Mock<ISpecialEventCache>().Object).GetParameters().Select(p => p.Key).Should().Equal("eventCode");
            new FirstPurchaseOnlyEvaluator(new Mock<ICompletedOrderCounter>().Object).GetParameters().Should().BeEmpty();

            var percent = new PercentageOffEvaluator(new CouponsOptions()).GetParameters().Single();
            percent.Min.Should().Be(0m);
            percent.Max.Should().Be(100m);
            new MinOrderValueEvaluator(new CouponsOptions()).GetParameters().Single().Min.Should().Be(0m);
            new PerProductScopeEvaluator().GetParameters().Single().Min.Should().Be(1m);
            new FixedAmountOffEvaluator().GetParameters().Single().Min.Should().Be(0m);
        }

        [Fact]
        public void OptionsDrivenDefaults_ShouldBeSharedByEvaluatorAndSchema()
        {
            var options = new CouponsOptions { DefaultMinOrderValue = 50m, DefaultDiscountPercent = 25m };
            var context = new CouponEvaluationContext(1, "user", 50m, new List<CouponEvaluationItem>());
            var min = new MinOrderValueEvaluator(options);
            var percent = new PercentageOffEvaluator(options);

            min.Evaluate(context, new Dictionary<string, string>()).Passed.Should().BeTrue();
            min.Evaluate(new CouponEvaluationContext(1, "user", 49m, new List<CouponEvaluationItem>()), new Dictionary<string, string>()).Passed.Should().BeFalse();
            percent.Evaluate(context, new Dictionary<string, string>()).Reduction.Should().Be(12.5m);
            min.GetParameters().Single().DefaultValue.Should().Be("50");
            percent.GetParameters().Single().DefaultValue.Should().Be("25");
        }

        /// <summary>
        /// Builds the registry through the real production wiring (<c>AddCouponRuleEngine</c>) rather
        /// than a hand-rolled fixture, so a rule that is added or dropped in composition is actually
        /// caught here instead of being mirrored by the test's own setup.
        /// </summary>
        private static ICouponRuleRegistry BuildProductionRegistry()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new Mock<ICouponUsedRepository>().Object);
            services.AddSingleton(new Mock<ISpecialEventCache>().Object);
            services.AddSingleton(new Mock<IStockAvailabilityChecker>().Object);
            services.AddSingleton(new Mock<ICompletedOrderCounter>().Object);
            services.AddSingleton(new CouponsOptions());
            services.AddCouponRuleEngine();

            return services.BuildServiceProvider().GetRequiredService<ICouponRuleRegistry>();
        }

        private static CouponService CreateServiceOver(ICouponRuleRegistry registry)
        {
            return new CouponService(
                new Mock<ICouponRepository>().Object,
                new Mock<ICouponUsedRepository>().Object,
                new Mock<IModuleClient>().Object,
                new Mock<ICouponsUnitOfWork>().Object,
                new Mock<IOutboxWriter>().Object,
                new Mock<IScopeTargetRepository>().Object,
                new Mock<ICouponRulePipeline>().Object,
                new CouponsOptions(),
                new Mock<ICouponApplicationRecordRepository>().Object,
                registry);
        }

        [Fact]
        public void Catalog_ShouldExposeTheRealRuleSet_AndFilterOutTheAutoInjectedGuard()
        {
            var registry = BuildProductionRegistry();

            // The guard really is registered — otherwise "not in the catalog" would prove nothing.
            registry.GetAllRules().Select(r => r.Name).Should().Contain(CouponRuleNames.OversizeGuard);

            var catalog = CreateServiceOver(registry).GetRuleCatalog();

            catalog.Categories.Single(c => c.Category == CouponRuleCategory.Scope).Rules.Select(r => r.Name)
                .Should().BeEquivalentTo(CouponRuleNames.OrderTotal, CouponRuleNames.PerProduct,
                    CouponRuleNames.PerCategory, CouponRuleNames.PerTag);

            catalog.Categories.Single(c => c.Category == CouponRuleCategory.Discount).Rules.Select(r => r.Name)
                .Should().BeEquivalentTo(CouponRuleNames.PercentageOff, CouponRuleNames.FixedAmountOff,
                    CouponRuleNames.FreeItem, CouponRuleNames.GiftProduct, CouponRuleNames.FreeCheapestItem);

            catalog.Categories.Single(c => c.Category == CouponRuleCategory.Constraint).Rules.Select(r => r.Name)
                .Should().BeEquivalentTo(CouponRuleNames.MaxUses, CouponRuleNames.MaxUsesPerUser,
                    CouponRuleNames.ValidDateRange, CouponRuleNames.MinOrderValue,
                    CouponRuleNames.SpecialEvent, CouponRuleNames.FirstPurchaseOnly);

            catalog.Categories.SelectMany(c => c.Rules).Select(r => r.Name)
                .Should().NotContain(CouponRuleNames.OversizeGuard);
        }

        [Fact]
        public void Catalog_ShouldCarryEveryRulesParameterSchema()
        {
            var catalog = CreateServiceOver(BuildProductionRegistry()).GetRuleCatalog();

            var percentageOff = catalog.Categories
                .SelectMany(c => c.Rules)
                .Single(r => r.Name == CouponRuleNames.PercentageOff);

            percentageOff.Parameters.Select(p => p.Key).Should().Equal("percent");
            percentageOff.Parameters.Single().DefaultValue.Should().Be("10");
        }

        [Fact]
        public void Catalog_RequiresScopeTargets_ShouldBeTrueForEveryScopeRuleExceptOrderTotal()
        {
            var catalog = CreateServiceOver(BuildProductionRegistry()).GetRuleCatalog();

            var scopeRules = catalog.Categories.Single(c => c.Category == CouponRuleCategory.Scope).Rules;

            // Mirrors Coupon.ValidateRuleComposition: order-total must have no targets, the rest require them.
            scopeRules.Single(r => r.Name == CouponRuleNames.OrderTotal).RequiresScopeTargets.Should().BeFalse();
            scopeRules.Single(r => r.Name == CouponRuleNames.PerProduct).RequiresScopeTargets.Should().BeTrue();
            scopeRules.Single(r => r.Name == CouponRuleNames.PerCategory).RequiresScopeTargets.Should().BeTrue();
            scopeRules.Single(r => r.Name == CouponRuleNames.PerTag).RequiresScopeTargets.Should().BeTrue();

            // No non-scope rule should ever claim to need targets.
            catalog.Categories.Where(c => c.Category != CouponRuleCategory.Scope)
                .SelectMany(c => c.Rules)
                .Should().OnlyContain(r => !r.RequiresScopeTargets);
        }
    }
}