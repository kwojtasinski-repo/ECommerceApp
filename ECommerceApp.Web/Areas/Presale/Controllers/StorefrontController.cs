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
        private readonly IProductTagService _tagService;

        public StorefrontController(IStorefrontQueryService storefront, IProductTagService tagService)
        {
            _storefront = storefront;
            _tagService = tagService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? searchString = null, CancellationToken ct = default)
        {
            var model = await _storefront.GetPublishedProductsAsync(12, 1, searchString ?? string.Empty, ct);
            ViewBag.AllTags = await _tagService.GetAllTags();
            return View(model);
        }

        [HttpPost("")]
        public async Task<IActionResult> Index(int pageSize, int pageNo, string? searchString, CancellationToken ct = default)
        {
            var model = await _storefront.GetPublishedProductsAsync(pageSize, pageNo, searchString ?? string.Empty, ct);
            ViewBag.AllTags = await _tagService.GetAllTags();
            return View(model);
        }

        [HttpGet("tag/{tagId:int}")]
        public async Task<IActionResult> ByTag(int tagId, int pageSize = 12, int pageNo = 1, CancellationToken ct = default)
        {
            var tag = await _tagService.GetTag(tagId);
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
