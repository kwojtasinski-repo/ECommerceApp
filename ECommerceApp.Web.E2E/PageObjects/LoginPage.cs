using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class LoginPage : ILoginPage
    {
        private readonly IPage _page;

        public LoginPage(IPage page)
        {
            _page = page;
        }

        public static async Task<LoginPage> NavigateAsync(IPage page, string baseAddress)
        {
            await page.GotoAsync($"{baseAddress}/Identity/Account/Login");
            return new LoginPage(page);
        }

        public async Task<ILoginPage> SubmitLogin(string email, string password)
        {
            await EmailInput().FillAsync(email);
            await PasswordInput().FillAsync(password);
            await SubmitButton().ClickAsync();
            return this;
        }

        public async Task<bool> IsDisplayed()
        {
            return await EmailInput().CountAsync() > 0;
        }

        public async Task<bool> HasValidationError()
        {
            return await ValidationSummary().CountAsync() > 0;
        }

        private ILocator EmailInput() => _page.Locator("input[type='email']");
        private ILocator PasswordInput() => _page.Locator("#Input_Password");
        private ILocator SubmitButton() => _page.Locator("#account button[type='submit']");
        private ILocator ValidationSummary() => _page.Locator("#account .validation-summary-errors");
    }
}