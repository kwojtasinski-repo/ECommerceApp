using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    public sealed class LoginPageTests
    {
        private readonly PlaywrightBrowserFixture _browserFixture;

        public LoginPageTests(PlaywrightBrowserFixture browserFixture)
        {
            _browserFixture = browserFixture;
        }

        [Fact]
        public async Task LoginPage_LoadsThroughKestrelAndPlaywright()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.StartKestrelHost();
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            ILoginPage loginPage = await LoginPage.NavigateAsync(page, factory.ServerAddress);

            (await loginPage.IsDisplayed()).ShouldBeTrue();
        }

        [Fact]
        public async Task LoginPage_SubmitLogin_BlankCredentials_ShowsValidationError()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            factory.StartKestrelHost();
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            ILoginPage loginPage = await LoginPage.NavigateAsync(page, factory.ServerAddress);
            loginPage = await loginPage.SubmitLogin(string.Empty, string.Empty);

            (await loginPage.HasValidationError()).ShouldBeTrue();
        }
    }
}
