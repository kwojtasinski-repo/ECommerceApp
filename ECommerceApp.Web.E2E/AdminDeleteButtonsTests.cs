using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using Shouldly;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    /// <summary>
    /// Regression guard: the delete links on the Currencies and IAM user lists call inline functions that
    /// depend on AMD modules captured from the DOMInitialized event. When the capture is missing the click
    /// throws a ReferenceError and nothing happens, so every test also asserts no uncaught page errors.
    /// </summary>
    public sealed class AdminDeleteButtonsTests
    {
        // ISO 4217 "test" code: valid for CurrencyCode but never used by seeded or other test data.
        private const string DisposableCurrencyCode = "XTS";

        private readonly PlaywrightBrowserFixture _browserFixture;

        public AdminDeleteButtonsTests(PlaywrightBrowserFixture browserFixture)
        {
            _browserFixture = browserFixture;
        }

        [Fact]
        public async Task CurrencyList_DeleteCurrency_ConfirmedInModal_RemovesRowWithoutPageErrors()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            await E2ESeed.CurrencyAsync(services, DisposableCurrencyCode);
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            var pageErrors = new List<string>();
            page.PageError += (_, error) => pageErrors.Add(error);
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            ICurrencyListPage currencyList = await CurrencyListPage.NavigateAsync(page, factory.ServerAddress);
            currencyList = await currencyList.SearchAsync(DisposableCurrencyCode);
            (await currencyList.ContainsCurrencyAsync(DisposableCurrencyCode)).ShouldBeTrue();
            currencyList = await currencyList.DeleteCurrencyAsync(DisposableCurrencyCode);
            currencyList = await currencyList.SearchAsync(DisposableCurrencyCode);

            (await currencyList.ContainsCurrencyAsync(DisposableCurrencyCode)).ShouldBeFalse();
            pageErrors.ShouldBeEmpty();
        }

        [Fact]
        public async Task UserManagement_DeleteUser_ConfirmedInModal_RemovesRowWithoutPageErrors()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var userName = await E2ESeed.DisposableUserAsync(services);
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            var pageErrors = new List<string>();
            page.PageError += (_, error) => pageErrors.Add(error);
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            IUserManagementPage userList = await UserManagementPage.NavigateAsync(page, factory.ServerAddress);
            (await userList.ContainsUserAsync(userName)).ShouldBeTrue();
            userList = await userList.DeleteUserAsync(userName);

            (await userList.ContainsUserAsync(userName)).ShouldBeFalse();
            pageErrors.ShouldBeEmpty();
        }
    }
}
