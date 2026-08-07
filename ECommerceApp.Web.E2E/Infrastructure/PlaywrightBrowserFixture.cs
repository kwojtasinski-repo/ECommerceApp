using Microsoft.Playwright;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E.Infrastructure
{
    public sealed class PlaywrightBrowserFixture : IAsyncLifetime
    {
        public IPlaywright Playwright { get; private set; } = default!;
        public IBrowser Browser { get; private set; } = default!;

        public async ValueTask InitializeAsync()
        {
            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
        }

        public async ValueTask DisposeAsync()
        {
            await Browser.DisposeAsync();
            Playwright.Dispose();
        }
    }
}
