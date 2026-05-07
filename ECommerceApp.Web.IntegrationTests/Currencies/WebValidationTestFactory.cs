using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Web;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.IntegrationTests
{
    /// <summary>
    /// Web application factory for MVC validation behavior tests.
    /// Extends <see cref="CustomWebApplicationFactory{TStartup}"/> directly (not BcWebApplicationFactory)
    /// so that Identity stores remain scoped and cookie-based login works correctly.
    ///
    /// Stubs <see cref="ICategoryService"/> because <c>_Layout.cshtml</c> always calls
    /// <c>GetAllCategories()</c> to populate the navigation menu, and the EF InMemory
    /// provider cannot coerce the <c>CategoryName</c> value object to <c>string</c>.
    /// </summary>
    public sealed class WebValidationTestFactory : CustomWebApplicationFactory<Startup>
    {
        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICategoryService));
            if (descriptor != null) services.Remove(descriptor);
            services.AddScoped<ICategoryService, NullCategoryService>();
        }
    }

    internal sealed class NullCategoryService : ICategoryService
    {
        public Task<int> AddCategory(Application.Catalog.Products.DTOs.CreateCategoryDto dto) => Task.FromResult(0);
        public Task<bool> UpdateCategory(Application.Catalog.Products.DTOs.UpdateCategoryDto dto) => Task.FromResult(false);
        public Task<bool> DeleteCategory(int id) => Task.FromResult(false);
        public Task<CategoryVm> GetCategory(int id) => Task.FromResult<CategoryVm>(null);
        public Task<List<CategoryVm>> GetAllCategories() => Task.FromResult(new List<CategoryVm>());
    }
}

