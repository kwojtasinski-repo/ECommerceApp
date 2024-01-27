using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Tag;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.API.Controllers
{
    [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
    [Route("api/tags")]
    [ApiController]
    public class TagController : ControllerBase
    {
        private readonly ITagService _tagService;

        public TagController(ITagService tagService)
        {
            _tagService = tagService;
        }

        [HttpGet]
        public ActionResult<List<TagDto>> GetItemTags()
        {
            return Ok(_tagService.GetTags());
        }

        [HttpGet("{id}")]
        public ActionResult<TagDetailsVm> GetTag(int id)
        {
            var tag = _tagService.GetTagById(id);
            if (tag == null)
            {
                return NotFound();
            }
            return Ok(tag);
        }

        [HttpPut]
        public IActionResult EditItemTag(TagDto model)
        {
            var modelExists = _tagService.TagExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _tagService.UpdateTag(model);
            return Ok();
        }

        [HttpPost]
        public IActionResult AddItemTag(TagDto model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var id = _tagService.AddTag(model);
            return Ok(id);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteItemTag(int id)
        {
            _tagService.DeleteTag(id);
            return Ok();
        }
    }
}
