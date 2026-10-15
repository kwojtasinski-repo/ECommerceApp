using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class CurrencyListPage : ICurrencyListPage
    {
        private readonly IPage _page;

        private CurrencyListPage(IPage page)
        {
            _page = page;
        }

        public static async Task<CurrencyListPage> NavigateAsync(IPage page, string baseAddress)
        {
            await page.GotoAsync($"{baseAddress}/Currencies/Currency");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            return new CurrencyListPage(page);
        }

        public async Task<ICurrencyListPage> SearchAsync(string searchString)
        {
            await _page.Locator("#searchString").FillAsync(searchString);
            await _page.RunAndWaitForNavigationAsync(
                () => _page.Locator("#search-form button[type='submit']").ClickAsync());
            return this;
        }

        public async Task<bool> ContainsCurrencyAsync(string code)
        {
            return await Row(code).CountAsync() > 0;
        }

        public async Task<ICurrencyListPage> DeleteCurrencyAsync(string code)
        {
            await Row(code).GetByText("Usuń", new LocatorGetByTextOptions { Exact = true }).ClickAsync();

            var confirmButton = _page.Locator(".modalClass .btn-danger").Filter(new LocatorFilterOptions { HasText = "Tak" });
            await confirmButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
            await _page.RunAndWaitForNavigationAsync(() => confirmButton.ClickAsync());
            return this;
        }

        private ILocator Row(string code) =>
            _page.Locator("tbody tr").Filter(new LocatorFilterOptions { HasText = code });
    }
}
