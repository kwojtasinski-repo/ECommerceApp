using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.Catalog.Products;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal sealed class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _repo;
        private readonly IMemoryCache _cache;

        public CategoryService(ICategoryRepository repo, IMemoryCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        public async Task<int> AddCategory(CreateCategoryDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(CreateCategoryDto)} cannot be null");

            var category = Category.Create(dto.Name);
            var id = await _repo.AddAsync(category);
            _cache.Remove(CachedCatalogNavigationService.AllCategoriesCacheKey);
            return id.Value;
        }

        public async Task<bool> UpdateCategory(UpdateCategoryDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(UpdateCategoryDto)} cannot be null");

            var category = await _repo.GetByIdAsync(new CategoryId(dto.Id));
            if (category is null)
                return false;

            category.Update(dto.Name);
            await _repo.UpdateAsync(category);
            _cache.Remove(CachedCatalogNavigationService.AllCategoriesCacheKey);
            return true;
        }

        public async Task<bool> DeleteCategory(int id)
        {
            var deleted = await _repo.DeleteAsync(new CategoryId(id));
            if (deleted)
                _cache.Remove(CachedCatalogNavigationService.AllCategoriesCacheKey);
            return deleted;
        }

        public async Task<CategoryVm> GetCategory(int id)
        {
            var category = await _repo.GetByIdAsync(new CategoryId(id));
            return category is null ? null : CategoryVm.FromDomain(category);
        }

        public async Task<List<CategoryVm>> GetAllCategories()
        {
            var categories = await _repo.GetAllAsync();
            return categories.Select(CategoryVm.FromDomain).ToList();
        }
    }
}
