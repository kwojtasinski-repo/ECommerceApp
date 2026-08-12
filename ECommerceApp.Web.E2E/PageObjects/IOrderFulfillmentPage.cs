using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IOrderFulfillmentPage
    {
        Task<IShipmentCreatePage> OpenCreateShipmentAsync();
    }
}