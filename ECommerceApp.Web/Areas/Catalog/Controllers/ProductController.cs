using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    public class ProductController : BaseController
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IProductTagService _tagService;

        public ProductController(IProductService productService, ICategoryService categoryService, IProductTagService tagService)
        {
            _productService = productService;
            _categoryService = categoryService;
            _tagService = tagService;
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!await _productService.ProductExists(id))
            {
                return NotFound();
            }
            var model = await _productService.GetProductDetails(id);
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> All()
        {
            var model = await _productService.GetAllProducts(20, 1, string.Empty);
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> All(int pageSize, int pageNo, string? searchString)
        {
            var model = await _productService.GetAllProducts(pageSize, pageNo, searchString ?? string.Empty);
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _categoryService.GetAllCategories();
            return View(new CreateProductFormVm());
        }

        [HttpPost]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> Create(CreateProductFormVm model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _categoryService.GetAllCategories();
                return View(model);
            }
            var tagIds = await ParseTagsAsync(model.Tags);
            var dto = new CreateProductDto(model.Name, model.Cost, model.Description, model.CategoryId, tagIds);
            var id = await _productService.AddProduct(dto);
            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpGet]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> Edit(int id)
        {
            if (!await _productService.ProductExists(id))
            {
                return NotFound();
            }
            var vm = await _productService.GetProductDetails(id);
            ViewBag.ProductId = id;
            ViewBag.Categories = await _categoryService.GetAllCategories();
            ViewBag.AllTags = await _tagService.GetAllTags();
            ViewBag.Images = vm.Images;
            var model = new UpdateProductFormVm
            {
                Name = vm.Name,
                Cost = vm.Cost,
                Description = vm.Description,
                CategoryId = vm.CategoryId,
                Tags = string.Join(", ", vm.TagNames)
            };
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> Edit(int id, UpdateProductFormVm model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ProductId = id;
                ViewBag.Categories = await _categoryService.GetAllCategories();
                ViewBag.AllTags = await _tagService.GetAllTags();
                ViewBag.Images = new List<ProductImageVm>();
                return View(model);
            }
            var tagIds = await ParseTagsAsync(model.Tags);
            var dto = new UpdateProductDto(id, model.Name, model.Cost, model.Description, model.CategoryId, tagIds);
            await _productService.UpdateProduct(dto);
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> Delete(int id)
        {
            await _productService.DeleteProduct(id);
            return RedirectToAction(nameof(All));
        }

        [HttpPost]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> Publish(int id)
        {
            await _productService.PublishProduct(id);
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> Unpublish(int id)
        {
            await _productService.UnpublishProduct(id);
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<List<int>> ParseTagsAsync(string? tags)
        {
            var ids = new List<int>();
            if (string.IsNullOrWhiteSpace(tags))
            {
                return ids;
            }
            foreach (var name in tags.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0))
            {
                var tag = await _tagService.GetOrCreateTag(name);
                ids.Add(tag.Id);
            }
            return ids;
        }
    }
}
