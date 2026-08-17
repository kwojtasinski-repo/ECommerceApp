using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IOrderAccessLookupPage
    {
        Task<IOrderAccessLookupPage> RequestAccessAsync(string email);
        Task<IOrderSummaryPage> ConfirmAccessAsync(string code);
        Task<string> GetMessageAsync();
    }
}
