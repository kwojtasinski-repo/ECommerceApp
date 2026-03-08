using ECommerceApp.Application.Presale.Checkout.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    public interface IStorefrontQueryService
    {
        Task<StorefrontProductListVm> GetPublishedProductsAsync(
            int pageSize, int pageNo, string searchString, CancellationToken ct = default);
    }
}
