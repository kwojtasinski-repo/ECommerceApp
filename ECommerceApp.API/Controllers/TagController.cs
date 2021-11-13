using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Item;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Route("api/tags")]
    [ApiController]
    public class TagController : ControllerBase
    {
        private readonly IItemService _itemService;

        public TagController(IItemService itemService)
        {
            _itemService = itemService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public ActionResult<List<TagVm>> GetItemTags()
        {
            var tags = _itemService.GetAllTags();
            if (tags.Count == 0)
            {
                return NotFound();
            }
            return Ok(tags);
        }

        [Authorize(Roles = "Administratorm, Admin, Manager, Service")]
        [HttpGet("{id}")]
        public ActionResult<TagDetailsVm> GetTag(int id)
        {
            var tag = _itemService.GetItemTagById(id);
            if (tag == null)
            {
                return NotFound();
            }
            return Ok(tag);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut]
        public IActionResult EditItemTag(TagDetailsVm model)
        {
            var modelExists = _itemService.CheckIfItemTagExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _itemService.UpdateItemTag(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult AddItemTag(TagDetailsVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItemTag(model);
            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItemTag(int id)
        {
            _itemService.DeleteItemTag(id);
            return RedirectToAction("Index");
        }
    }
}
