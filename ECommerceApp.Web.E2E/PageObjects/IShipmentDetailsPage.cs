using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IShipmentDetailsPage
    {
        Task<IShipmentDetailsPage> DispatchAsync();
        Task<IShipmentDetailsPage> DeliverAsync();
        Task<string> GetStatusAsync();
    }
}