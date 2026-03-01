using System.Threading.Tasks;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Route("v2/categories")]
    public class V2CategoryController : Controller
    {
        private readonly ICategoryService _categoryService;

        public V2CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index() =>
            View(await _categoryService.GetAllCategories());

        [HttpGet("add")]
        public IActionResult Add() => View();

        [HttpPost("add")]
        public async Task<IActionResult> Add(CategoryFormVm form)
        {
            await _categoryService.AddCategory(new CreateCategoryDto(form.Name));
            TempData["Success"] = "Category added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _categoryService.GetCategory(id);
            return vm is null ? NotFound() : View(vm);
        }

        [HttpPost("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id, CategoryFormVm form)
        {
            await _categoryService.UpdateCategory(new UpdateCategoryDto(id, form.Name));
            TempData["Success"] = "Category updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _categoryService.DeleteCategory(id);
            TempData["Success"] = "Category deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CategoryFormVm form)
        {
            if (string.IsNullOrWhiteSpace(form?.Name))
                return BadRequest(new { error = "Name is required." });

            var trimmed = form.Name.Trim();
            var id = await _categoryService.AddCategory(new CreateCategoryDto(trimmed));
            return Json(new { id, name = trimmed });
        }
    }
}
