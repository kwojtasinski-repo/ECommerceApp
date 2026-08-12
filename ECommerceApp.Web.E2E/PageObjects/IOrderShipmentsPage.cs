using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IOrderShipmentsPage
    {
        Task<IShipmentDetailsPage> OpenLatestShipmentDetailsAsync();
    }
}