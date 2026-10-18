using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    public sealed class ManageEmailTests
    {
        private readonly PlaywrightBrowserFixture _browserFixture;

        public ManageEmailTests(PlaywrightBrowserFixture browserFixture)
        {
            _browserFixture = browserFixture;
        }

        [Fact]
        public async Task ManageEmail_ValidNewEmail_ShowsConfirmationSentWithoutPageErrors()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            var services = factory.StartKestrelHost();
            var userEmail = await E2ESeed.DisposableUserAsync(services);
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            var pageErrors = new List<string>();
            page.PageError += (_, error) => pageErrors.Add(error);
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(userEmail, E2ESeed.Password);

            IManageEmailPage emailPage = await ManageEmailPage.NavigateAsync(page, factory.ServerAddress);
            emailPage = await emailPage.ChangeEmailAsync($"new-{Guid.NewGuid():N}@example.com");

            (await emailPage.GetStatusMessageAsync()).ShouldContain("Confirmation link to change email sent");
            pageErrors.ShouldBeEmpty();
        }

        [Fact]
        public async Task ManageEmail_InvalidNewEmail_ShowsFieldErrorClientSide()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            var services = factory.StartKestrelHost();
            var userEmail = await E2ESeed.DisposableUserAsync(services);
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(userEmail, E2ESeed.Password);

            IManageEmailPage emailPage = await ManageEmailPage.NavigateAsync(page, factory.ServerAddress);
            emailPage = await emailPage.ChangeEmailAsync("not-an-email");

            (await emailPage.HasFieldValidationErrorAsync()).ShouldBeTrue();
        }
    }
}
