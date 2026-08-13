using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IOrderFulfillmentPage
    {
        Task<IShipmentCreatePage> OpenCreateShipmentAsync();

        /// <summary>
        /// The order status as the admin sees it — the rendered <c>OrderStatus</c> name, e.g.
        /// <c>Placed</c> before payment and <c>PaymentConfirmed</c> after it.
        /// </summary>
        Task<string> GetOrderStatusAsync();
    }
}