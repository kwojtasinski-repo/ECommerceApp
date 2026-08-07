using ECommerceApp.Web.E2E.Infrastructure;
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

            await page.GotoAsync($"{_factory.ServerAddress}/Identity/Account/Login");

            (await page.Locator("input[type='email']").CountAsync()).ShouldBeGreaterThan(0);
        }
    }
}
