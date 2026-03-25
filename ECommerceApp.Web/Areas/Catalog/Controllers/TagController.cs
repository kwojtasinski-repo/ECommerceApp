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
    public class TagController : BaseController
    {
        private readonly IProductTagService _tagService;

        public TagController(IProductTagService tagService)
        {
            _tagService = tagService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _tagService.GetTagsWithUsageAsync(5);
            return View(model);
        }

        [HttpGet]
        public IActionResult Create() => View(new TagFormVm());

        [HttpPost]
        public async Task<IActionResult> Create(TagFormVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            await _tagService.AddTag(new CreateTagDto(model.Name));
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var tag = await _tagService.GetTag(id);
            if (tag is null)
            {
                return NotFound();
            }
            return View(new TagFormVm { Name = tag.Name });
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, TagFormVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            await _tagService.UpdateTag(new UpdateTagDto(id, model.Name));
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _tagService.DeleteTag(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
