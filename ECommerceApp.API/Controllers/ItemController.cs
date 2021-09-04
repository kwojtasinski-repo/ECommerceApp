using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Item;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Route("api/items")]
    [Authorize]
    [ApiController]
    public class ItemController : ControllerBase
    {
        private readonly ItemServiceAbstract _itemService;

        public ItemController(ItemServiceAbstract itemService)
        {
            _itemService = itemService;
        }

        [HttpGet("all")]
        public ActionResult<List<ItemForListVm>> GetItems()
        {
            var items = _itemService.GetAllItems();
            if (items.Count == 0)
            {
                return NotFound();
            }
            return Ok(items);
        }

        [HttpGet("{id}")]
        public ActionResult<ItemDetailsVm> GetItem(int id)
        {
            var item = _itemService.GetItemDetails(id);
            if (item == null)
            {
                return NotFound();
            }
            return Ok(item);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut]
        public IActionResult EditItem(ItemVm model)
        {
            var modelExists = _itemService.CheckIfItemExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            var item = model.MapToNewItemVm();
            _itemService.UpdateItem(item);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult AddItem(ItemVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var item = model.MapToNewItemVm();
            _itemService.AddItem(item);
            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItem(int id)
        {
            _itemService.DeleteItem(id);
            return RedirectToAction("Index");
        }
    }
}
