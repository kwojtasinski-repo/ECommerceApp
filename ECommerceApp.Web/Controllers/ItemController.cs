using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Brands;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Item;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Web.Controllers
{
    public class ItemController : BaseController
    {
        private readonly IItemService _itemService;
        private readonly IBrandService _brandService;
        private readonly ITypeService _typeService;
        private readonly ITagService _tagService;

        public ItemController(IItemService itemService, IBrandService brandService, ITypeService typeService, ITagService tagService)
        {
            _itemService = itemService;
            _brandService = brandService;
            _typeService = typeService;
            _tagService = tagService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = _itemService.GetAllAvailableItemsForList(20, 1, "");
            return View(model);
        }

        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var model = _itemService.GetAllAvailableItemsForList(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult AddItem()
        {
            return View(new NewItemVm()
            {
                Brands = _brandService.GetAllBrands().ToList(),
                Types = _typeService.GetTypes().ToList(),
                Tags = _tagService.GetTags().ToList()
            });
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult AddItem(NewItemVm model)
        {
            try
            {
                var id = _itemService.AddItem(new AddItemDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    Cost = model.Cost,
                    Quantity = model.Quantity,
                    Warranty = model.Warranty,
                    BrandId = model.BrandId,
                    TypeId = model.TypeId,
                    TagsId = model.ItemTags,
                    Images = model.Images.Select(i => new AddItemImageDto(i.Name, i.ImageSource))
                });
                return RedirectToAction("EditItem", new { id });
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "AddItem", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult EditItem(int id)
        {
            var item = _itemService.GetItemDetails(id);
            if (item is null)
            {
                var errorModel = BuildErrorModel("itemNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new EditItemVm(new ItemDetailsDto()));
            }
            return View(new EditItemVm(item)
            {
                Brands = _brandService.GetAllBrands().ToList(),
                Types = _typeService.GetTypes().ToList(),
                Tags = _tagService.GetTags().ToList()
            });
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult EditItem(EditItemVm model)
        {
            try
            {
                if (!_itemService.UpdateItem(new UpdateItemDto
                {
                    Id = model.Item.Id,
                    Name = model.Item.Name,
                    Description = model.Item.Description,
                    Cost = model.Item.Cost,
                    Quantity = model.Item.Quantity,
                    Warranty = model.Item.Warranty,
                    BrandId = model.Item.Brand.Id,
                    TypeId = model.Item.Type.Id,
                    TagsId = model.ItemTags
                }))
                {
                    var errorModel = BuildErrorModel("itemNotFound", new Dictionary<string, string> { { "id", $"{model.Item.Id}" } });
                    return RedirectToAction("Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
                }
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult ShowItemConnectedWithTags()
        {
            var tag = _itemService.GetAllItemsWithTags(20, 1, "");
            return View(tag);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult ShowItemConnectedWithTags(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var tag = _itemService.GetAllItemsWithTags(pageSize, pageNo.Value, searchString);
            return View(tag);
        }

        [HttpGet]
        public IActionResult ViewItem(int id)
        {
            var item = _itemService.GetItemDetails(id);
            if (item is null)
            {
                var errorModel = BuildErrorModel("itemNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new ItemDetailsDto());
            }
            return View(item);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        public IActionResult DeleteItem(int id)
        {
            try
            {
                return _itemService.DeleteItem(id)
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
