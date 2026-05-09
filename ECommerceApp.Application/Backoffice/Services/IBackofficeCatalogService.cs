using ECommerceApp.Application.Backoffice.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    public interface IBackofficeCatalogService
    {
        Task<BackofficeCatalogListVm> GetProductsAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default);
        Task<BackofficeCatalogDetailVm> GetProductDetailAsync(int productId, CancellationToken ct = default);
    }
}
