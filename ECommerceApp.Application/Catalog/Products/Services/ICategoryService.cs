using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    public interface ICategoryService
    {
        Task<int> AddCategory(CreateCategoryDto dto);
        Task<bool> UpdateCategory(UpdateCategoryDto dto);
        Task<bool> DeleteCategory(int id);
        Task<CategoryVm> GetCategory(int id);
        Task<List<CategoryVm>> GetAllCategories();
    }
}
