using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Tag;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult EditTag(int id)
        {
            var tag = _tagService.GetTagById(id);
            if (tag is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("tagNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
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
                    var errorModel = BuildErrorModel(ErrorCode.Create("tagNotFound", ErrorParameter.Create("id", model.Tag.Id)));
                    return RedirectToAction("Index", errorModel.AsOjectRoute());
                }
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult ViewTag(int id)
        {
            var tag = _tagService.GetTagDetails(id);
            if (tag is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("tagNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
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
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }
    }
}
