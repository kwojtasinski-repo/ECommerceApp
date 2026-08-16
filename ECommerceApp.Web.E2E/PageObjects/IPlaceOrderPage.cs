using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IPlaceOrderPage
    {
        Task<IPlaceOrderPage> FillCustomerAsync();
        Task<IPlaceOrderPage> FillGuestCustomerAsync(string email);
        Task<IOrderSummaryPage> SubmitAsync();
    }
}