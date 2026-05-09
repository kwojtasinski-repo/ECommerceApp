using ECommerceApp.Application.Catalog.Products.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    public interface ICatalogNavigationService
    {
        Task<List<CategoryVm>> GetAllCategories();
    }
}
