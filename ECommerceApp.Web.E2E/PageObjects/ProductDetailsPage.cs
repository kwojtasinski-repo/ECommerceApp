using Microsoft.Playwright;
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
            await _page.Locator("#addToCartForm button[type='submit']").ClickAsync();
            await _page.WaitForURLAsync($"**/offers/{productId}");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
    }
}