using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IStorefrontPage
    {
        Task<IProductDetailsPage> OpenProductAsync(string productName);
    }
}