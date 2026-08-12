using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.PageObjects
{
    public interface IProductDetailsPage
    {
        Task AddToCartAsync(int productId, int quantity);
    }
}