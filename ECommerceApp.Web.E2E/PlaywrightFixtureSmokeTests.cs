using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    [Collection(PlaywrightCollection.Name)]
    public sealed class PlaywrightFixtureSmokeTests : IClassFixture<PlaywrightWebApplicationFactory>
    {
        private readonly PlaywrightBrowserFixture _browserFixture;
        private readonly PlaywrightWebApplicationFactory _factory;

        public PlaywrightFixtureSmokeTests(
            PlaywrightBrowserFixture browserFixture,
            PlaywrightWebApplicationFactory factory)
        {
            _browserFixture = browserFixture;
            _factory = factory;
        }

        [Fact]
        public async Task LoginPage_LoadsThroughKestrelAndPlaywright()
        {
            _factory.StartKestrelHost();
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            ILoginPage loginPage = await LoginPage.NavigateAsync(page, _factory.ServerAddress);

            (await loginPage.IsDisplayed()).ShouldBeTrue();
        }

        [Fact]
        public async Task LoginPage_SubmitLogin_BlankCredentials_ShowsValidationError()
        {
            _factory.StartKestrelHost();
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            ILoginPage loginPage = await LoginPage.NavigateAsync(page, _factory.ServerAddress);
            loginPage = await loginPage.SubmitLogin(string.Empty, string.Empty);

            (await loginPage.HasValidationError()).ShouldBeTrue();
        }
    }
}
