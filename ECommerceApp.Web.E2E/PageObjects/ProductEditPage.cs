using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class ProductEditPage : IProductEditPage
    {
        private readonly IPage _page;

        private ProductEditPage(IPage page)
        {
            _page = page;
        }

        public static async Task<ProductEditPage> NavigateAsync(IPage page, string baseAddress, int productId)
        {
            await page.GotoAsync($"{baseAddress}/Catalog/Product/Edit/{productId}");
            await page.WaitForFunctionAsync("() => document.getElementById('chunkUploadWidget')?.dataset.ready === 'true'");
            return new ProductEditPage(page);
        }

        public Task ChooseFileAsync(string filePath) =>
            _page.Locator("#chunkFileInput").SetInputFilesAsync(filePath);

        public Task ClickUploadAsync() =>
            _page.Locator("#chunkUploadBtn").ClickAsync();

        public Task<string> WaitForUploadErrorAsync() =>
            WaitForVisibleTextAsync("#chunkUploadError");

        public Task<string> WaitForResumeNoticeAsync() =>
            WaitForVisibleTextAsync("#chunkUploadInfo");

        public Task WaitForPageToSettleAsync() =>
            _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        public Task<int> GalleryImageCountAsync() =>
            _page.Locator("[data-name='ItemImage'] img").CountAsync();

        public async Task WaitForGalleryImageCountAsync(int expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (await GalleryImageCountAsync() != expected)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException($"Expected {expected} image(s) in the product edit gallery.");
                }

                await Task.Delay(100);
            }
        }

        private async Task<string> WaitForVisibleTextAsync(string selector)
        {
            var locator = _page.Locator(selector);
            await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
            return (await locator.TextContentAsync()) ?? string.Empty;
        }
    }
}
