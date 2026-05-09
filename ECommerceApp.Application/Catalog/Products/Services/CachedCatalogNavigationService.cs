using ECommerceApp.Application.Catalog.Products.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal sealed class CachedCatalogNavigationService : ICatalogNavigationService
    {
        private readonly ICategoryService _categoryService;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
        private const string AllCategoriesCacheKey = "CatalogNavigation:AllCategories";

        public CachedCatalogNavigationService(ICategoryService categoryService, IMemoryCache cache)
        {
            _categoryService = categoryService;
            _cache = cache;
        }

        public async Task<List<CategoryVm>> GetAllCategories()
        {
            if (!_cache.TryGetValue(AllCategoriesCacheKey, out List<CategoryVm> categories))
            {
                categories = await _categoryService.GetAllCategories();
                _cache.Set(AllCategoriesCacheKey, categories, CacheDuration);
            }
            return categories;
        }
    }
}
