using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class ManageEmailPage : IManageEmailPage
    {
        private readonly IPage _page;

        private ManageEmailPage(IPage page)
        {
            _page = page;
        }

        public static async Task<ManageEmailPage> NavigateAsync(IPage page, string baseAddress)
        {
            await page.GotoAsync($"{baseAddress}/Identity/Account/Manage/Email");
            return new ManageEmailPage(page);
        }

        public async Task<IManageEmailPage> ChangeEmailAsync(string newEmail)
        {
            await _page.Locator("#Input_NewEmail").FillAsync(newEmail);
            await _page.Locator("#change-email-button").ClickAsync();
            return this;
        }

        public async Task<string> GetStatusMessageAsync()
        {
            var status = _page.Locator(".alert:has-text('email')").First;
            await status.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
            return await status.InnerTextAsync();
        }

        public async Task<bool> HasFieldValidationErrorAsync()
        {
            var error = _page.Locator("span[data-valmsg-for='Input.NewEmail']");
            return await error.CountAsync() > 0 && !string.IsNullOrWhiteSpace(await error.InnerTextAsync());
        }
    }
}
