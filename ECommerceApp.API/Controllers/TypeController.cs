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
    [Route("api/types")]
    [ApiController]
    public class TypeController : ControllerBase
    {
        private readonly ItemServiceAbstract _itemService;

        public TypeController(ItemServiceAbstract itemService)
        {
            _itemService = itemService;
        }

        [HttpGet("all")]
        public ActionResult<List<TypeForListVm>> GetItemTypes()
        {
            var types = _itemService.GetAllItemTypes();
            if (types.Count == 0)
            {
                return NotFound();
            }
            return Ok(types);
        }

        [HttpGet("{id}")]
        public ActionResult<NewItemTypeVm> GetType(int id)
        {
            var type = _itemService.GetItemTypeById(id);
            if (type == null)
            {
                return NotFound();
            }
            return Ok(type);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut]
        public IActionResult EditItemType(NewItemTypeVm model)
        {
            var modelExists = _itemService.CheckIfItemTypeExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _itemService.UpdateItemType(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult AddItemType(NewItemTypeVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItemType(model);
            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItemType(int id)
        {
            _itemService.DeleteItemType(id);
            return RedirectToAction("Index");
        }
    }
}
