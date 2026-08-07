using Xunit;

namespace ECommerceApp.Web.E2E.Infrastructure
{
    [CollectionDefinition(Name)]
    public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightBrowserFixture>
    {
        public const string Name = "Playwright";
    }
}
