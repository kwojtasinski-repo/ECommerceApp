using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Application.Constants;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    internal sealed class CachedCatalogNavigationService : ICatalogNavigationService
    {
        private readonly ICategoryService _categoryService;
        private readonly IMemoryCache _cache;
        private readonly CacheOptions _cacheOptions;
        public const string AllCategoriesCacheKey = "CatalogNavigation:AllCategories";

        public CachedCatalogNavigationService(ICategoryService categoryService, IMemoryCache cache, IOptions<CacheOptions> cacheOptions)
        {
            _categoryService = categoryService;
            _cache = cache;
            _cacheOptions = cacheOptions.Value;
        }

        public async Task<List<CategoryVm>> GetAllCategories()
        {
            if (!_cache.TryGetValue(AllCategoriesCacheKey, out List<CategoryVm> categories))
            {
                categories = await _categoryService.GetAllCategories();
                _cache.Set(AllCategoriesCacheKey, categories, _cacheOptions.CatalogNavigationTtl);
            }
            return categories;
        }
    }
}
