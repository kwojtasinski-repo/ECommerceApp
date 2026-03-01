using System.Threading.Tasks;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Route("v2/tags")]
    public class V2TagController : Controller
    {
        private readonly IProductTagService _tagService;

        public V2TagController(IProductTagService tagService)
        {
            _tagService = tagService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index() =>
            View(await _tagService.GetTagsWithUsageAsync());

        [HttpGet("add")]
        public IActionResult Add() => View();

        [HttpPost("add")]
        public async Task<IActionResult> Add(TagFormVm form)
        {
            await _tagService.AddTag(new CreateTagDto(form.Name));
            TempData["Success"] = "Tag added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _tagService.GetTag(id);
            return vm is null ? NotFound() : View(vm);
        }

        [HttpPost("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id, TagFormVm form)
        {
            await _tagService.UpdateTag(new UpdateTagDto(id, form.Name));
            TempData["Success"] = "Tag updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _tagService.DeleteTag(id);
            TempData["Success"] = "Tag deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
