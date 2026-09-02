using AwesomeAssertions;
using ECommerceApp.Application.Sales.Coupons;
using ECommerceApp.Application.Sales.Coupons.Contracts;
using ECommerceApp.Application.Sales.Coupons.Rules;
using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Web.Areas.Sales;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Linq;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Coupons
{
    /// <summary>
    /// The admin-facing builder is generated from the rule registry, so a rule added without a Polish
    /// display name would silently reach the UI as raw kebab-case. These tests walk the real registry
    /// rather than a hardcoded list, so adding a rule fails the build until it is translated.
    /// </summary>
    public class CouponRuleDisplayNamesTests
    {
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

        [Fact]
        public void EverySelectableRule_ShouldHaveAPolishDisplayName()
        {
            var selectable = BuildProductionRegistry().GetAllRules()
                .Where(r => r.Name != CouponRuleNames.OversizeGuard)
                .Select(r => r.Name);

            foreach (var ruleName in selectable)
            {
                CouponRuleDisplayNames.ForRule(ruleName).Should().NotBe(ruleName,
                    $"rule '{ruleName}' is offered in the builder but has no Polish display name");
            }
        }

        [Fact]
        public void EveryParameterKey_ShouldHaveAPolishLabel()
        {
            var parameterKeys = BuildProductionRegistry().GetAllRules()
                .Where(r => r.Name != CouponRuleNames.OversizeGuard && r.ParameterSchema != null)
                .SelectMany(r => r.ParameterSchema.GetParameters())
                .Select(p => p.Key)
                .Distinct();

            foreach (var key in parameterKeys)
            {
                CouponRuleDisplayNames.ForParameter(key).Should().NotBe(key,
                    $"parameter '{key}' is rendered in the builder but has no Polish label");
            }
        }

        [Fact]
        public void EveryCategory_ShouldHaveAPolishDisplayName()
        {
            foreach (var category in new[]
            {
                CouponRuleCategory.Scope,
                CouponRuleCategory.Discount,
                CouponRuleCategory.Constraint
            })
            {
                CouponRuleDisplayNames.ForCategory(category).Should().NotBe(category.ToString());
            }
        }
    }
}
