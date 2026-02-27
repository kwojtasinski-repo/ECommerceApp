using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Route("v2/products")]
    public class V2ProductController : Controller
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IProductTagService _tagService;

        public V2ProductController(
            IProductService productService,
            ICategoryService categoryService,
            IProductTagService tagService)
        {
            _productService = productService;
            _categoryService = categoryService;
            _tagService = tagService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index() =>
            View(await _productService.GetAllProducts(50, 1, string.Empty));

        [HttpGet("details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var vm = await _productService.GetProductDetails(id);
            return vm is null ? NotFound() : View(vm);
        }

        [HttpGet("add")]
        public async Task<IActionResult> Add()
        {
            ViewBag.Categories = await _categoryService.GetAllCategories();
            ViewBag.Tags = await _tagService.GetAllTags();
            return View();
        }

        [HttpPost("add")]
        public async Task<IActionResult> Add(
            string name, decimal cost, int quantity, string description,
            int categoryId, int[] tagIds)
        {
            await _productService.AddProduct(
                new CreateProductDto(name, cost, quantity, description, categoryId, tagIds ?? new int[0]));
            TempData["Success"] = "Product added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _productService.GetProductDetails(id);
            if (vm is null) return NotFound();
            ViewBag.Categories = await _categoryService.GetAllCategories();
            ViewBag.Tags = await _tagService.GetAllTags();
            return View(vm);
        }

        [HttpPost("edit/{id:int}")]
        public async Task<IActionResult> Edit(
            int id, string name, decimal cost, int quantity,
            string description, int categoryId, int[] tagIds)
        {
            await _productService.UpdateProduct(
                new UpdateProductDto(id, name, cost, quantity, description, categoryId, tagIds ?? new int[0]));
            TempData["Success"] = "Product updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _productService.DeleteProduct(id);
            TempData["Success"] = "Product deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("publish/{id:int}")]
        public async Task<IActionResult> Publish(int id)
        {
            await _productService.PublishProduct(id);
            TempData["Success"] = "Product published.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("unpublish/{id:int}")]
        public async Task<IActionResult> Unpublish(int id)
        {
            await _productService.UnpublishProduct(id);
            TempData["Success"] = "Product unpublished.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
