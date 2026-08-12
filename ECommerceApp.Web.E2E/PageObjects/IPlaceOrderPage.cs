using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IPlaceOrderPage
    {
        Task<IPlaceOrderPage> FillCustomerAsync();
        Task<IOrderSummaryPage> SubmitAsync();
    }
}