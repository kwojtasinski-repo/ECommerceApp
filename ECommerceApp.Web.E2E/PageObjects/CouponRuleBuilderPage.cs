using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public sealed class CouponRuleBuilderPage : ICouponRuleBuilderPage
    {
        private readonly IPage _page;

        private CouponRuleBuilderPage(IPage page)
        {
            _page = page;
        }

        public static async Task<CouponRuleBuilderPage> NavigateAsync(IPage page, string baseAddress)
        {
            var pageError = string.Empty;
            page.PageError += (_, error) => pageError = error;
            await page.GotoAsync($"{baseAddress}/Sales/Coupon/Create");
            try
            {
                await page.WaitForFunctionAsync("() => document.getElementById('couponRuleBuilder')?.dataset.ready === 'true'");
            }
            catch (System.TimeoutException ex)
            {
                throw new System.InvalidOperationException($"Coupon builder did not initialize. Page error: {pageError}", ex);
            }
            return new CouponRuleBuilderPage(page);
        }

        public async Task SelectScopeAsync(string ruleName)
        {
            await _page.SelectOptionAsync("#Rules_Scope_Name", ruleName);
        }

        public async Task SelectDiscountAsync(string ruleName)
        {
            await _page.SelectOptionAsync("#Rules_Discount_Name", ruleName);
        }

        public async Task FillParameterAsync(string slot, string key, string value)
        {
            await _page.Locator($"[name='Rules.{slot}.Parameters[{key}]']").FillAsync(value);
        }

        public async Task SelectScopeTargetAsync(int targetId)
        {
            await _page.SelectOptionAsync("#scopeTargetSelect", targetId.ToString());
        }

        public async Task SubmitAsync(string code, string description)
        {
            await _page.Locator("input[name='Code']").FillAsync(code);
            await _page.Locator("input[name='Description']").FillAsync(description);
            await _page.Locator("#couponCreateForm button[type='submit']").ClickAsync();

            // ClickAsync returns once the click is dispatched, not once the POST has been handled.
            // Without this wait a caller that asserts against the database races the request and the
            // test passes or fails depending on timing. Both outcomes navigate — a success redirects
            // to Index, a validation failure re-renders Create — so waiting for the load to settle is
            // correct for either.
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        public ILocator ValidationSummary() => _page.Locator("#validationSummary");
        public ILocator ScopeTargetSelect() => _page.Locator("#scopeTargetSelect");
        public string Url => _page.Url;
    }
}
