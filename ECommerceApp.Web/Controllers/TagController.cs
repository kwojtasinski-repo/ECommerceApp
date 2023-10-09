using System;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Tag;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class TagController : Controller
    {
        private readonly ITagService _tagService;
        public TagController(ITagService tagService)
        {
            _tagService = tagService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var tag = _tagService.GetTags(20, 1, "");
            return View(tag);
        }

        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var tag = _tagService.GetTags(pageSize, pageNo.Value, searchString);
            return View(tag);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult AddTag()
        {
            var tag = new TagVm();
            return View(tag);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult AddTag(TagVm model)
        {
            var id = _tagService.AddTag(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult EditTag(int id)
        {
            var tag = _tagService.GetTagById(id);
            if (tag is null)
            {
                return NotFound();
            }
            return View(tag);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult EditTag(TagVm model)
        {
            _tagService.UpdateTag(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult ViewTag(int id)
        {
            var tag = _tagService.GetTagDetails(id);
            if (tag is null)
            {
                return NotFound();
            }
            return View(tag);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        public IActionResult DeleteTag(int id)
        {
            _tagService.DeleteTag(id);
            return RedirectToAction("Index");
        }
    }
}
