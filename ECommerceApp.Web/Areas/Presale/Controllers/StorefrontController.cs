using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Presale.Controllers
{
    [Area("Presale")]
    [Route("offers")]
    public class StorefrontController : BaseController
    {
        private readonly IStorefrontQueryService _storefront;
        private readonly ICatalogNavigationService _navigation;

        public StorefrontController(IStorefrontQueryService storefront, ICatalogNavigationService navigation)
        {
            _storefront = storefront;
            _navigation = navigation;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string searchString = null, int? categoryId = null, CancellationToken ct = default)
        {
            var model = await _storefront.GetPublishedProductsAsync(12, 1, searchString ?? string.Empty, categoryId, ct);
            ViewBag.AllTags = await _storefront.GetAllTagsAsync(ct);
            ViewBag.AllCategories = await _navigation.GetAllCategories();
            ViewBag.SelectedCategoryId = categoryId;
            return View(model);
        }

        [HttpPost("")]
        public async Task<IActionResult> Index(int pageSize, int pageNo, string searchString, int? categoryId, CancellationToken ct = default)
        {
            var model = await _storefront.GetPublishedProductsAsync(pageSize, pageNo, searchString ?? string.Empty, categoryId, ct);
            ViewBag.AllTags = await _storefront.GetAllTagsAsync(ct);
            ViewBag.AllCategories = await _navigation.GetAllCategories();
            ViewBag.SelectedCategoryId = categoryId;
            return View(model);
        }

        [HttpGet("tag/{tagId:int}")]
        public async Task<IActionResult> ByTag(int tagId, int pageSize = 12, int pageNo = 1, CancellationToken ct = default)
        {
            var tag = await _storefront.GetTagByIdAsync(tagId, ct);
            if (tag is null)
            {
                return NotFound();
            }
            var model = await _storefront.GetPublishedProductsByTagAsync(tagId, pageSize, pageNo, ct);
            ViewBag.TagName = tag.Name;
            ViewBag.TagId = tagId;
            return View(model);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id, CancellationToken ct = default)
        {
            var model = await _storefront.GetProductDetailsAsync(id, ct);
            if (model is null)
            {
                return NotFound();
            }
            return View(model);
        }
    }
}
