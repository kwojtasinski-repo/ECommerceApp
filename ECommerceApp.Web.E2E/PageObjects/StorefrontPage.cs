using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class StorefrontPage : IStorefrontPage
    {
        private readonly IPage _page;

        private StorefrontPage(IPage page)
        {
            _page = page;
        }

        public static async Task<StorefrontPage> NavigateAsync(IPage page, string baseAddress)
        {
            var response = await page.GotoAsync($"{baseAddress}/offers?e2eRefresh={Guid.NewGuid():N}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            if (response is null || !response.Ok)
            {
                var responseBody = response is null ? string.Empty : await response.TextAsync();
                var responseExcerpt = responseBody.Length <= 2000
                    ? responseBody
                    : responseBody[..1000] + " ... " + responseBody[^1000..];
                throw new InvalidOperationException(
                    $"Storefront navigation failed. URL: {page.Url}; status: {response?.Status}; body: {responseExcerpt}");
            }

            return new StorefrontPage(page);
        }

        public async Task<IProductDetailsPage> OpenProductAsync(string productName)
        {
            var productCard = _page.Locator(".card").Filter(new LocatorFilterOptions { HasText = productName });
            if (await productCard.CountAsync() == 0)
            {
                var baseAddress = new Uri(_page.Url).GetLeftPart(UriPartial.Authority);
                await _page.GotoAsync($"{baseAddress}/offers?searchString={Uri.EscapeDataString(productName)}&e2eRefresh={Guid.NewGuid():N}");
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                productCard = _page.Locator(".card").Filter(new LocatorFilterOptions { HasText = productName });
            }

            if (await productCard.CountAsync() == 0)
            {
                var bodyText = await _page.Locator("body").InnerTextAsync();
                var html = await _page.ContentAsync();
                throw new InvalidOperationException(
                    $"Product '{productName}' was not rendered. URL: {_page.Url}; title: {await _page.TitleAsync()}; htmlLength: {html.Length}; body: {bodyText[..Math.Min(bodyText.Length, 500)]}");
            }

            await productCard.GetByRole(AriaRole.Link, new() { Name = "Szczegóły" }).ClickAsync();
            await _page.WaitForURLAsync("**/offers/**");
            return new ProductDetailsPage(_page);
        }

        public async Task<IProductDetailsPage> OpenProductAsync(int productId)
        {
            var baseAddress = new Uri(_page.Url).GetLeftPart(UriPartial.Authority);
            var response = await _page.GotoAsync($"{baseAddress}/offers/{productId}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            if (response is null || !response.Ok)
            {
                throw new InvalidOperationException(
                    $"Product details navigation failed. Product ID: {productId}; URL: {_page.Url}; status: {response?.Status}");
            }

            return new ProductDetailsPage(_page);
        }
    }
}