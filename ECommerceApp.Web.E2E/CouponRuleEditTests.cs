using ECommerceApp.Domain.Sales.Coupons;
using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    public sealed class CouponRuleEditTests
    {
        private readonly PlaywrightBrowserFixture _browserFixture;

        public CouponRuleEditTests(PlaywrightBrowserFixture browserFixture)
        {
            _browserFixture = browserFixture;
        }

        [Fact]
        public async Task Edit_ExistingCoupon_ShouldHydrateRulesAndParameters()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var coupon = await SeedCouponAsync(services, "E2E-EDIT-HYDRATE", "15");
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            await page.GotoAsync($"{factory.ServerAddress}/Sales/Coupon/Edit/{coupon.Id.Value}");
            (await page.Locator("#couponRuleBuilder").CountAsync()).ShouldBe(1);
            await page.WaitForFunctionAsync("() => document.getElementById('couponRuleBuilder')?.dataset.ready === 'true'");

            (await page.Locator("#Rules_Scope_Name").InputValueAsync()).ShouldBe("order-total");
            (await page.Locator("#Rules_Discount_Name").InputValueAsync()).ShouldBe("percentage-off");
            (await page.Locator("[name='Rules.Discount.Parameters[percent]']").InputValueAsync()).ShouldBe("15");
        }

        [Fact]
        public async Task Edit_ChangedDiscount_ShouldPersistRulesJson()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var coupon = await SeedCouponAsync(services, "E2E-EDIT-DISCOUNT", "15");
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            await page.GotoAsync($"{factory.ServerAddress}/Sales/Coupon/Edit/{coupon.Id.Value}");
            await page.WaitForFunctionAsync("() => document.getElementById('couponRuleBuilder')?.dataset.ready === 'true'");
            await page.Locator("[name='Rules.Discount.Parameters[percent]']").FillAsync("25");
            await page.Locator("#couponEditForm button[type='submit']").ClickAsync();
            // Wait for the POST to be handled before reading the database, otherwise the assertion
            // races the request and the result depends on timing rather than on behaviour.
            await page.WaitForURLAsync(url => !url.Contains("/Edit/"));

            using var assertScope = services.CreateScope();
            var updated = await assertScope.ServiceProvider.GetRequiredService<ICouponRepository>().GetByCodeAsync("E2E-EDIT-DISCOUNT");
            CouponRuleDefinition.ParseList(updated.RulesJson)
                .Single(rule => rule.Name == "percentage-off").Parameters["percent"].ShouldBe("25");
        }

        [Fact]
        public async Task Edit_OrderTotalToPerProduct_ShouldPersistScopeTarget()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productIds = await E2ESeed.BasketProductsAsync(services);
            var coupon = await SeedCouponAsync(services, "E2E-EDIT-SCOPE", "15");
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            await page.GotoAsync($"{factory.ServerAddress}/Sales/Coupon/Edit/{coupon.Id.Value}");
            await page.WaitForFunctionAsync("() => document.getElementById('couponRuleBuilder')?.dataset.ready === 'true'");
            await page.SelectOptionAsync("#Rules_Scope_Name", "per-product");
            // Drive the real picker. Injecting the hidden fields by hand instead would add a second
            // input named Rules.Scope.Parameters[productId] alongside the empty one the builder
            // renders, and the duplicate binds as an unparseable value — so the payload would be
            // rejected by the very validation this test is meant to pass through.
            await page.WaitForFunctionAsync(
                "() => document.getElementById('scopeTargetSelect')?.options.length > 1");
            await page.SelectOptionAsync("#scopeTargetSelect", productIds[0].ToString());
            await page.Locator("#couponEditForm button[type='submit']").ClickAsync();
            await page.WaitForURLAsync(url => !url.Contains("/Edit/"));

            using var assertScope = services.CreateScope();
            var target = (await assertScope.ServiceProvider.GetRequiredService<IScopeTargetRepository>()
                .GetByScopeTypeAndTargetIdAsync("per-product", productIds[0])).Single();
            target.ScopeType.Value.ShouldBe("per-product");
            target.TargetId.ShouldBe(productIds[0]);
        }

        private static async Task<Coupon> SeedCouponAsync(IServiceProvider services, string code, string percent)
        {
            using var scope = services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICouponRepository>();
            var coupon = Coupon.CreateWithRules(
                code,
                "E2E edit coupon",
                CouponRuleDefinition.SerializeList(new[]
                {
                    new CouponRuleDefinition("order-total", CouponRuleCategory.Scope, new Dictionary<string, string>()),
                    new CouponRuleDefinition("percentage-off", CouponRuleCategory.Discount, new Dictionary<string, string> { ["percent"] = percent })
                }),
                new List<CouponScopeTarget>());
            await repository.AddAsync(coupon);
            return await repository.GetByCodeAsync(code);
        }
    }
}
