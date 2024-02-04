using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Tag;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ECommerceApp.API.Controllers
{
    [Authorize(Roles = $"{MaintenanceRole}")]
    [Route("api/tags")]
    public class TagController : BaseController
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

        [HttpPut("{id:int}")]
        public IActionResult EditItemTag(int id, TagDto model)
        {
            model.Id = id;
            if (!ModelState.IsValid)
            {
                return Conflict(ModelState);
            }
            return _tagService.UpdateTag(model)
                ? Ok()
                : NotFound();
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

        [HttpDelete("{id:int}")]
        public IActionResult DeleteItemTag(int id)
        {
            return _tagService.DeleteTag(id) 
                ? Ok()
                : NotFound();
        }
    }
}
