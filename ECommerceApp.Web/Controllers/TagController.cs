using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Tag;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ECommerceApp.Web.Controllers
{
    [Authorize]
    public class TagController : BaseController
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

            searchString ??= string.Empty;
            var tag = _tagService.GetTags(pageSize, pageNo.Value, searchString);
            return View(tag);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult AddTag()
        {
            var tag = new TagVm() { Tag = new TagDto() };
            return View(tag);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult AddTag(TagVm model)
        {
            try
            {
                _tagService.AddTag(model.Tag);
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                var errorModel = BuildErrorModel(exception.ErrorCode, exception.Arguments);
                return RedirectToAction("Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult EditTag(int id)
        {
            var tag = _tagService.GetTagById(id);
            if (tag is null)
            {
                var errorModel = BuildErrorModel("tagNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new TagVm() { Tag = new TagDto() });
            }
            return View(new TagVm { Tag = tag });
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult EditTag(TagVm model)
        {
            try
            {
                if (!_tagService.UpdateTag(model.Tag))
                {
                    var errorModel = BuildErrorModel("tagNotFound", new Dictionary<string, string> { { "id", $"{model.Tag.Id}" } });
                    return RedirectToAction("Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
                }
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                var errorModel = BuildErrorModel(exception.ErrorCode, exception.Arguments);
                return RedirectToAction("Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult ViewTag(int id)
        {
            var tag = _tagService.GetTagDetails(id);
            if (tag is null)
            {
                var errorModel = BuildErrorModel("tagNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new TagDetailsVm());
            }
            return View(tag);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        public IActionResult DeleteTag(int id)
        {
            try
            {
                return _tagService.DeleteTag(id)
                    ? Json("deleted")
                    : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }
    }
}
