using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class ProductDetailsPage : IProductDetailsPage
    {
        private readonly IPage _page;

        internal ProductDetailsPage(IPage page)
        {
            _page = page;
        }

        public async Task AddToCartAsync(int productId, int quantity)
        {
            await _page.Locator("#addToCartForm input[name='quantity']").FillAsync(quantity.ToString());

            // Storefront/Details.cshtml's submit handler POSTs via fetch and only *afterwards* assigns
            // window.location.href = response.url, where response.url is this very page (the POST's
            // returnUrl). So neither the POST response nor WaitForURL marks the end of the interaction:
            // the POST response arrives long before the assignment runs, and the URL glob is already
            // satisfied by the page we are standing on. Both let the caller run ahead and issue its next
            // GotoAsync, which Chromium then aborts in favour of the late client-side navigation
            // (net::ERR_ABORTED). A committed same-document-origin navigation replaces `window`, so the
            // sentinel disappearing is the one signal that the reload actually happened.
            await _page.EvaluateAsync("() => { window.__e2eAddToCartPending = true; }");
            await _page.Locator("#addToCartForm button[type='submit']").ClickAsync();
            try
            {
                await _page.WaitForFunctionAsync("() => window.__e2eAddToCartPending === undefined");
            }
            catch (PlaywrightException)
            {
                throw new InvalidOperationException(
                    $"Add to cart did not reload the product page for product {productId} (quantity {quantity}). " +
                    $"The POST likely failed and the page stayed put. URL: {_page.Url}");
            }

            await _page.WaitForURLAsync($"**/offers/{productId}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
    }
}