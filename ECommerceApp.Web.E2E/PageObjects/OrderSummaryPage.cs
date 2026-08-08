using Microsoft.Playwright;
using Shouldly;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class OrderSummaryPage
    {
        private readonly IPage _page;

        public OrderSummaryPage(IPage page)
        {
            _page = page;
        }

        public async Task ShouldConfirmOrderAsync()
        {
            (await _page.GetByRole(AriaRole.Heading, new() { Name = "Zamówienie złożone!" }).CountAsync())
                .ShouldBe(1);
            (await _page.Locator("strong").InnerTextAsync()).ShouldNotBeNullOrWhiteSpace();
        }
    }
}