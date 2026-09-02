using Microsoft.Playwright;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface ICouponRuleBuilderPage
    {
        Task SelectScopeAsync(string ruleName);
        Task SelectDiscountAsync(string ruleName);
        Task FillParameterAsync(string slot, string key, string value);
        Task SelectScopeTargetAsync(int targetId);
        Task SubmitAsync(string code, string description);
        ILocator ValidationSummary();
        ILocator ScopeTargetSelect();
        string Url { get; }
    }
}
