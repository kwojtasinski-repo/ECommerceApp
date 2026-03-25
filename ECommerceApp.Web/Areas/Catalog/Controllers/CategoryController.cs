using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Authorize(Roles = MaintenanceRole)]
    public class CategoryController : BaseController
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _categoryService.GetAllCategories();
            return View(model);
        }

        [HttpGet]
        public IActionResult Create() => View(new CategoryFormVm());

        [HttpPost]
        public async Task<IActionResult> Create(CategoryFormVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            await _categoryService.AddCategory(new CreateCategoryDto(model.Name));
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _categoryService.GetCategory(id);
            if (category is null)
            {
                return NotFound();
            }
            return View(new CategoryFormVm { Name = category.Name });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, CategoryFormVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            await _categoryService.UpdateCategory(new UpdateCategoryDto(id, model.Name));
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _categoryService.DeleteCategory(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
