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

        [HttpGet("get/all")]
        public ActionResult<List<ItemForListVm>> GetItems()
        {
            var items = _itemService.GetAllItems();
            if (items.Count == 0)
            {
                return NotFound();
            }
            return Ok(items);
        }

        [HttpGet("get/brand/all")]
        public ActionResult<List<BrandForListVm>> GetItemBrands()
        {
            var brands = _itemService.GetAllItemBrands();
            if (brands.Count == 0)
            {
                return NotFound();
            }
            return Ok(brands);
        }

        [HttpGet("get/type/all")]
        public ActionResult<List<TypeForListVm>> GetItemTypes()
        {
            var types = _itemService.GetAllItemTypes();
            if (types.Count == 0)
            {
                return NotFound();
            }
            return Ok(types);
        }

        
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet("get/tag/all")]
        public ActionResult<List<TagForListVm>> GetItemTags()
        {
            var tags = _itemService.GetAllTags();
            if (tags.Count == 0)
            {
                return NotFound();
            }
            return Ok(tags);
        }

        [HttpGet("get/{id}")]
        public ActionResult<ItemDetailsVm> GetItem(int id)
        {
            var item = _itemService.GetItemDetails(id);
            if (item == null)
            {
                return NotFound();
            }
            return Ok(item);
        }

        [HttpGet("get/brand/{id}")]
        public ActionResult<NewItemBrandVm> GetBrand(int id)
        {
            var brand = _itemService.GetItemBrandById(id);
            if (brand == null)
            {
                return NotFound();
            }
            return Ok(brand);
        }

        [HttpGet("get/type/{id}")]
        public ActionResult<NewItemTypeVm> GetType(int id)
        {
            var type = _itemService.GetItemTypeById(id);
            if (type == null)
            {
                return NotFound();
            }
            return Ok(type);
        }

        [Authorize(Roles = "Administratorm, Admin, Manager, Service")]
        [HttpGet("get/tag/{id}")]
        public ActionResult<NewTagVm> GetTag(int id)
        {
            var tag = _itemService.GetItemTagById(id);
            if (tag == null)
            {
                return NotFound();
            }
            return Ok(tag);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut("edit/{id}")]
        public IActionResult EditItem(NewItemVm model)
        {
            var modelExists = _itemService.CheckIfItemExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _itemService.UpdateItem(model);
            return Ok();
        }

        
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut("edit/brand/{id}")]
        public IActionResult EditItemBrand(NewItemBrandVm model)
        {
            var modelExists = _itemService.CheckIfItemBrandExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _itemService.UpdateItemBrand(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut("edit/type/{id}")]
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
        [HttpPut("edit/tag/{id}")]
        public IActionResult EditItemTag(NewTagVm model)
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
        [HttpPost("add")]
        public IActionResult AddItem(NewItemVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItem(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost("add/brand")]
        public IActionResult AddItemBrand(NewItemBrandVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItemBrand(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost("add/type")]
        public IActionResult AddItemType(NewItemTypeVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItemType(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost("add/tag")]
        public IActionResult AddItemTag(NewTagVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItemTag(model);
            return Ok();
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItem(int id)
        {
            _itemService.DeleteItem(id);
            return RedirectToAction("Index");
        }

        [HttpDelete("delete/type/{id}")]
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItemType(int id)
        {
            _itemService.DeleteItemType(id);
            return RedirectToAction("Index");
        }

        [HttpDelete("delete/brand/{id}")]
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItemBrand(int id)
        {
            _itemService.DeleteItemBrand(id);
            return RedirectToAction("Index");
        }

        [HttpDelete("delete/tag/{id}")]
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItemTag(int id)
        {
            _itemService.DeleteItemTag(id);
            return RedirectToAction("Index");
        }
    }
}
