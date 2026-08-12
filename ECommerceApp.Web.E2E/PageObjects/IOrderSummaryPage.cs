using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IOrderSummaryPage
    {
        Task ShouldConfirmOrderAsync();
        Task<int> GetOrderIdAsync();
        Task<IPaymentPage> OpenPaymentAsync();
    }
}