using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Item;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class ItemController : ControllerBase
    {
        private readonly IItemService _itemService;

        public ItemController(IItemService itemService)
        {
            _itemService = itemService;
        }

        [HttpGet("Item/All")]
        public ActionResult<List<ItemForListVm>> GetItems()
        {
            var items = _itemService.GetAllItems();
            if (items.Count == 0)
            {
                return NotFound();
            }
            return Ok(items);
        }

        [HttpGet("Brand/All")]
        public ActionResult<List<BrandForListVm>> GetItemBrands()
        {
            var brands = _itemService.GetAllItemBrands();
            if (brands.Count == 0)
            {
                return NotFound();
            }
            return Ok(brands);
        }

        [HttpGet("Type/All")]
        public ActionResult<List<TypeForListVm>> GetItemTypes()
        {
            var types = _itemService.GetAllItemTypes();
            if (types.Count == 0)
            {
                return NotFound();
            }
            return Ok(types);
        }

        
        [Authorize(Roles = "Administrator, Manager, Service")]
        [HttpGet("Tag/All")]
        public ActionResult<List<TagForListVm>> GetItemTags()
        {
            var tags = _itemService.GetAllTags();
            if (tags.Count == 0)
            {
                return NotFound();
            }
            return Ok(tags);
        }

        [HttpGet("Item/Get/{id}")]
        public ActionResult<ItemDetailsVm> GetItem(int id)
        {
            var item = _itemService.GetItemDetails(id);
            if (item == null)
            {
                return NotFound();
            }
            return Ok(item);
        }

        [HttpGet("Brand/Get/{id}")]
        public ActionResult<NewItemBrandVm> GetBrand(int id)
        {
            var brand = _itemService.GetItemBrandById(id);
            if (brand == null)
            {
                return NotFound();
            }
            return Ok(brand);
        }

        [HttpGet("Type/Get/{id}")]
        public ActionResult<NewItemTypeVm> GetType(int id)
        {
            var type = _itemService.GetItemTypeById(id);
            if (type == null)
            {
                return NotFound();
            }
            return Ok(type);
        }

        [Authorize(Roles = "Administratorm, Manager, Service")]
        [HttpGet("Tag/Get/{id}")]
        public ActionResult<NewTagVm> GetTag(int id)
        {
            var tag = _itemService.GetItemTagById(id);
            if (tag == null)
            {
                return NotFound();
            }
            return Ok(tag);
        }

        [Authorize(Roles = "Administrator, Manager, Service")]
        [HttpPut("Item/Edit/{id}")]
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

        
        [Authorize(Roles = "Administrator, Manager, Service")]
        [HttpPut("Brand/Edit/{id}")]
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

        [Authorize(Roles = "Administrator, Manager, Service")]
        [HttpPut("Type/Edit/{id}")]
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

        [Authorize(Roles = "Administrator, Manager, Service")]
        [HttpPut("Tag/Edit/{id}")]
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


        [Authorize(Roles = "Administrator, Manager, Service")]
        [HttpPost("Item/New")]
        public IActionResult AddItem(NewItemVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItem(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Manager, Service")]
        [HttpPost("Brand/New")]
        public IActionResult AddItemBrand(NewItemBrandVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItemBrand(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Manager, Service")]
        [HttpPost("Type/New")]
        public IActionResult AddItemType(NewItemTypeVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItemType(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Manager, Service")]
        [HttpPost("Tag/New")]
        public IActionResult AddItemTag(NewTagVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            _itemService.AddItemTag(model);
            return Ok();
        }

        [HttpDelete("Item/Delete/{id}")]
        [Authorize(Roles = "Administrator, Manager, Service")]
        public IActionResult DeleteItem(int id)
        {
            _itemService.DeleteItem(id);
            return RedirectToAction("Index");
        }

        [HttpDelete("Type/Delete/{id}")]
        [Authorize(Roles = "Administrator, Manager, Service")]
        public IActionResult DeleteItemType(int id)
        {
            _itemService.DeleteItemType(id);
            return RedirectToAction("Index");
        }

        [HttpDelete("Brand/Delete/{id}")]
        [Authorize(Roles = "Administrator, Manager, Service")]
        public IActionResult DeleteItemBrand(int id)
        {
            _itemService.DeleteItemBrand(id);
            return RedirectToAction("Index");
        }

        [HttpDelete("Tag/Delete/{id}")]
        [Authorize(Roles = "Administrator, Manager, Service")]
        public IActionResult DeleteItemTag(int id)
        {
            _itemService.DeleteItemTag(id);
            return RedirectToAction("Index");
        }
    }
}
