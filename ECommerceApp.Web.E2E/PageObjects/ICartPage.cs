using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface ICartPage
    {
        Task<ICartPage> ShouldContainProductAsync(string productName, int quantity);
        Task<IPlaceOrderPage> ProceedToOrderAsync();
    }
}