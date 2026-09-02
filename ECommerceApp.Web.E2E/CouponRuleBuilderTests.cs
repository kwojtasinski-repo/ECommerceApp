using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    public sealed class CouponRuleBuilderTests
    {
        private readonly PlaywrightBrowserFixture _browserFixture;
        private readonly PlaywrightSqlFixture _sqlFixture;

        public CouponRuleBuilderTests(PlaywrightBrowserFixture browserFixture, PlaywrightSqlFixture sqlFixture)
        {
            _browserFixture = browserFixture;
            _sqlFixture = sqlFixture;
        }

        [Fact]
        public async Task Builder_OrderTotalPercentage_PersistsCanonicalRules()
        {
            var factory = _sqlFixture;
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            await E2ESeed.AdminAsync(services);
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);
            var builder = await CouponRuleBuilderPage.NavigateAsync(page, factory.ServerAddress);
            await builder.SelectScopeAsync("order-total");
            await builder.SelectDiscountAsync("percentage-off");
            await builder.FillParameterAsync("Discount", "percent", "15");
            await builder.SubmitAsync("E2E-RULE-15", "E2E percentage coupon");

            var coupon = await services.GetRequiredService<ICouponRepository>().GetByCodeAsync("E2E-RULE-15");
            coupon.ShouldNotBeNull();
            // Positive proof this ran against a real SQL Server, not InMemory: InMemory never populates
            // rowversion columns, so this assertion cannot pass under the provider phase 22 replaced.
            coupon.Version.ShouldNotBeNull();
            var rules = CouponRuleDefinition.ParseList(coupon.RulesJson);
            rules.Count.ShouldBe(2);
            rules.Single(r => r.Name == "percentage-off").Parameters["percent"].ShouldBe("15");
        }

        [Fact]
        public async Task Builder_PerProductScope_PersistsRuleAndTarget()
        {
            var factory = _sqlFixture;
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);
            var builder = await CouponRuleBuilderPage.NavigateAsync(page, factory.ServerAddress);
            await builder.SelectScopeAsync("per-product");
            await builder.SelectDiscountAsync("percentage-off");
            await builder.FillParameterAsync("Scope", "productId", productIds[0].ToString());
            await builder.SelectScopeTargetAsync(productIds[0]);
            await builder.FillParameterAsync("Discount", "percent", "10");
            await builder.SubmitAsync("E2E-PRODUCT-10", "E2E product coupon");

            var coupon = await services.GetRequiredService<ICouponRepository>().GetByCodeAsync("E2E-PRODUCT-10");
            coupon.ShouldNotBeNull();
            var target = (await services.GetRequiredService<IScopeTargetRepository>()
                .GetByScopeTypeAndTargetIdAsync("per-product", productIds[0])).Single();
            target.TargetId.ShouldBe(productIds[0]);
            target.ScopeType.Value.ShouldBe("per-product");
        }

        [Fact]
        public async Task Builder_RequiredParameter_BlocksSubmission()
        {
            var factory = _sqlFixture;
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            await E2ESeed.AdminAsync(services);
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);
            var builder = await CouponRuleBuilderPage.NavigateAsync(page, factory.ServerAddress);
            await builder.SelectScopeAsync("order-total");
            await builder.SelectDiscountAsync("fixed-amount-off");
            await builder.SubmitAsync("E2E-MISSING", "missing amount");

            builder.Url.ShouldContain("/Sales/Coupon/Create");
        }

        [Fact]
        public async Task Builder_ZeroAmount_ShowsServerValidation()
        {
            var factory = _sqlFixture;
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            await E2ESeed.AdminAsync(services);
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);
            var builder = await CouponRuleBuilderPage.NavigateAsync(page, factory.ServerAddress);
            await builder.SelectScopeAsync("order-total");
            await builder.SelectDiscountAsync("fixed-amount-off");
            await builder.FillParameterAsync("Discount", "amount", "0");
            await builder.SubmitAsync("E2E-ZERO", "zero amount");

            (await builder.ValidationSummary().InnerTextAsync()).ShouldContain("amount");
            (await services.GetRequiredService<ICouponRepository>().GetByCodeAsync("E2E-ZERO")).ShouldBeNull();
        }

        [Fact]
        public async Task Builder_TamperedCategory_DerivesCategoryFromRegistry()
        {
            var factory = _sqlFixture;
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            await E2ESeed.AdminAsync(services);
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);
            var builder = await CouponRuleBuilderPage.NavigateAsync(page, factory.ServerAddress);
            await builder.SelectScopeAsync("order-total");
            await builder.SelectDiscountAsync("percentage-off");
            await builder.FillParameterAsync("Discount", "percent", "20");
            await page.EvaluateAsync("() => { const input = document.createElement('input'); input.name = 'Rules.Discount.Category'; input.value = 'Scope'; document.querySelector('form').appendChild(input); }");
            await builder.SubmitAsync("E2E-CATEGORY", "tampered category");

            var coupon = await services.GetRequiredService<ICouponRepository>().GetByCodeAsync("E2E-CATEGORY");
            coupon.ShouldNotBeNull();
            CouponRuleDefinition.ParseList(coupon.RulesJson).Single(r => r.Name == "percentage-off").Category.ShouldBe(CouponRuleCategory.Discount);
        }
    }
}
