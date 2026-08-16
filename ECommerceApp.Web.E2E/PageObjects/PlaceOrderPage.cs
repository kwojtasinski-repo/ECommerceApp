using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class PlaceOrderPage : IPlaceOrderPage
    {
        private readonly IPage _page;

        internal PlaceOrderPage(IPage page)
        {
            _page = page;
        }

        public async Task<IPlaceOrderPage> FillCustomerAsync()
        {
            await _page.WaitForFunctionAsync("() => Number(document.getElementById('CustomerId').value) > 0");
            return await FillCustomerFieldsAsync("jan.e2e@example.com");
        }

        public async Task<IPlaceOrderPage> FillGuestCustomerAsync(string email)
        {
            return await FillCustomerFieldsAsync(email);
        }

        private async Task<IPlaceOrderPage> FillCustomerFieldsAsync(string email)
        {
            await _page.Locator("#FirstName").FillAsync("Jan");
            await _page.Locator("#LastName").FillAsync("Kowalski");
            await _page.Locator("#Email").FillAsync(email);
            await _page.Locator("#PhoneNumber").FillAsync("+48123456789");
            await _page.Locator("#Street").FillAsync("Testowa");
            await _page.Locator("#BuildingNumber").FillAsync("1");
            await _page.Locator("#ZipCode").FillAsync("00-001");
            await _page.Locator("#City").FillAsync("Warszawa");
            await _page.Locator("#Country").FillAsync("Poland");
            return this;
        }

        public async Task<IOrderSummaryPage> SubmitAsync()
        {
            await _page.GetByRole(AriaRole.Button, new() { Name = "Zamów" }).ClickAsync();
            await _page.WaitForURLAsync("**/Presale/Checkout/Summary/*");
            return new OrderSummaryPage(_page);
        }
    }
}