using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class UserManagementPage : IUserManagementPage
    {
        private readonly IPage _page;

        private UserManagementPage(IPage page)
        {
            _page = page;
        }

        public static async Task<UserManagementPage> NavigateAsync(IPage page, string baseAddress)
        {
            await page.GotoAsync($"{baseAddress}/IAM/UserManagement");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            return new UserManagementPage(page);
        }

        public async Task<bool> ContainsUserAsync(string userName)
        {
            return await Row(userName).CountAsync() > 0;
        }

        public async Task<IUserManagementPage> DeleteUserAsync(string userName)
        {
            await Row(userName).GetByText("Usuń", new LocatorGetByTextOptions { Exact = true }).ClickAsync();

            var confirmButton = _page.Locator(".modalClass .btn-danger").Filter(new LocatorFilterOptions { HasText = "Tak" });
            await confirmButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
            await _page.RunAndWaitForNavigationAsync(() => confirmButton.ClickAsync());
            return this;
        }

        private ILocator Row(string userName) =>
            _page.Locator("tbody tr").Filter(new LocatorFilterOptions { HasText = userName });
    }
}
