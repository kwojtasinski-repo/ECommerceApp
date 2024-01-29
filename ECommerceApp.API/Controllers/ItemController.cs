using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Item;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/items")]
    public class ItemController : BaseController
    {
        private readonly IItemService _itemService;

        public ItemController(IItemService itemService)
        {
            _itemService = itemService;
        }

        [HttpGet]
        public ActionResult<ListForItemVm> GetItems([FromQuery] int pageSize = 20, int pageNo = 1, string searchString = "")
        {
            var items = _itemService.GetAllItemsForList(pageSize, pageNo, searchString);
            if (items.Items.Count == 0)
            {
                return NotFound();
            }
            return Ok(items);
        }

        [HttpGet("{id}")]
        public ActionResult<ItemDetailsDto> GetItem(int id)
        {
            var item = _itemService.GetItemDetails(id);
            if (item == null)
            {
                return NotFound();
            }
            return Ok(item);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPut("{id:int}")]
        public IActionResult EditItem(int id, UpdateItemDto model)
        {
            model.Id = id;
            var modelExists = _itemService.ItemExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _itemService.UpdateItem(model);
            return Ok();
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult AddItem(AddItemDto model)
        {
            if (!ModelState.IsValid)
            {
                return Conflict(ModelState);
            }
            var id = _itemService.AddItem(model);
            return Ok(id);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpDelete("{id}")]
        public IActionResult DeleteItem(int id)
        {
            _itemService.DeleteItem(id);
            return Ok();
        }
    }
}
