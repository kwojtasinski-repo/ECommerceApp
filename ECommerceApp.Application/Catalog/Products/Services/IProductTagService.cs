using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    public interface IProductTagService
    {
        Task<int> AddTag(CreateTagDto dto);
        Task<bool> UpdateTag(UpdateTagDto dto);
        Task<bool> DeleteTag(int id);
        Task<ProductTagVm> GetTag(int id);
        Task<List<ProductTagVm>> GetAllTags();
        Task<List<ProductTagVm>> GetVisibleTags();
        Task<List<ProductTagVm>> SearchTags(string query, int maxResults = 10);
        Task<ProductTagVm> GetOrCreateTag(string name);
    }
}
