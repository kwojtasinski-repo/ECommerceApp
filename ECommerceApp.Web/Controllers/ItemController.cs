using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class ItemController : Controller
    {
        private readonly IItemService _itemService;
        public ItemController(IItemService itemService)
        {
            _itemService = itemService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = _itemService.GetAllItemsForList(20, 1, "");

            return View(model);
        }

        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var model = _itemService.GetAllItemsForList(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [HttpGet]
        public IActionResult AddItem()
        {   
            ViewBag.ItemBrands = _itemService.GetAllItemBrandsForAddingItems().ToList();
            ViewBag.ItemTypes = _itemService.GetAllItemTypesForAddingItems().ToList();
            ViewBag.ItemTags = _itemService.GetAllItemTagsForAddingItems().ToList();
            return View(new NewItemVm());
        }

        [HttpPost]
        public IActionResult AddItem(NewItemVm model)
        {
            var id = _itemService.AddItem(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult AddItemBrand()
        {
            return View(new NewItemBrandVm());
        }

        [HttpPost]
        public IActionResult AddItemBrand(NewItemBrandVm model)
        {
            var id = _itemService.AddItemBrand(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult AddItemType()
        {
            return View(new NewItemTypeVm());
        }

        [HttpPost]
        public IActionResult AddItemType(NewItemTypeVm model)
        {
            var id = _itemService.AddItemType(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult AddItemTag()
        {
            return View(new NewTagVm());
        }

        [HttpPost]
        public IActionResult AddItemTag(NewTagVm model)
        {
            var id = _itemService.AddItemTag(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditItem(int id)
        {
            var item = _itemService.GetItemById(id);
            ViewBag.ItemBrands = _itemService.GetAllItemBrandsForAddingItems().ToList();
            ViewBag.ItemTypes = _itemService.GetAllItemTypesForAddingItems().ToList();
            ViewBag.ItemTags = _itemService.GetAllItemTagsForAddingItems().ToList();
            return View(item);
        }

        [HttpPost]
        public IActionResult EditItem(NewItemVm model)
        {
            _itemService.UpdateItem(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditItemBrand(int id)
        {
            var item = _itemService.GetItemBrandById(id);
            return View(item);
        }

        [HttpPost]
        public IActionResult EditItemBrand(NewItemBrandVm model)
        {
            _itemService.UpdateItemBrand(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditItemType(int id)
        {
            var item = _itemService.GetItemTypeById(id);
            return View(item);
        }

        [HttpPost]
        public IActionResult EditItemType(NewItemTypeVm model)
        {
            _itemService.UpdateItemType(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditItemTag(int id)
        {
            var tag = _itemService.GetItemTagById(id);
            return View(tag);
        }

        [HttpPost]
        public IActionResult EditItemTag(NewTagVm model)
        {
            _itemService.UpdateItemTag(model);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult ShowItemBrands()
        {
            var brand = _itemService.GetAllItemBrands(20, 1, "");
            return View(brand);
        }

        [HttpPost]
        public IActionResult ShowItemBrands(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var item = _itemService.GetAllItemBrands(pageSize, pageNo.Value, searchString);
            return View(item);
        }

        [HttpGet]
        public IActionResult ShowItemTypes()
        {
            var type = _itemService.GetAllItemTypes(20, 1, "");
            return View(type);
        }

        [HttpPost]
        public IActionResult ShowItemTypes(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var type = _itemService.GetAllItemTypes(pageSize, pageNo.Value, searchString);
            return View(type);
        }

        [HttpGet]
        public IActionResult ShowItemTags()
        {
            var tag = _itemService.GetAllTags(20, 1, "");
            return View(tag);
        }

        [HttpPost]
        public IActionResult ShowItemTags(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var tag = _itemService.GetAllTags(pageSize, pageNo.Value, searchString);
            return View(tag);
        }

        [HttpGet]
        public IActionResult ShowItemConnectedWithTags()
        {
            var tag = _itemService.GetAllItemsWithTags(20, 1, "");
            return View(tag);
        }

        [HttpPost]
        public IActionResult ShowItemConnectedWithTags(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var tag = _itemService.GetAllItemsWithTags(pageSize, pageNo.Value, searchString);
            return View(tag);
        }

        [HttpGet]
        public IActionResult ViewItem(int id)
        {
            var item = _itemService.GetItemDetails(id);
            return View(item);
        }

        [HttpGet]
        public IActionResult ViewItemBrand(int id)
        {
            var item = _itemService.GetItemBrandById(id);
            return View(item);
        }

        [HttpGet] 
        public IActionResult ViewItemType(int id)
        {
            var item = _itemService.GetItemTypeById(id);
            return View(item);
        }

        [HttpGet]
        public IActionResult ViewItemTag(int id)
        {
            var item = _itemService.GetItemTagById(id);
            return View(item);
        }

        public IActionResult DeleteItem(int id)
        {
            _itemService.DeleteItem(id);
            return RedirectToAction("Index");
        }

        public IActionResult DeleteItemType(int id)
        {
            _itemService.DeleteItemType(id);
            return RedirectToAction("Index");
        }

        public IActionResult DeleteItemBrand(int id)
        {
            _itemService.DeleteItemBrand(id);
            return RedirectToAction("Index");
        }

        public IActionResult DeleteItemTag(int id)
        {
            _itemService.DeleteItemTag(id);
            return RedirectToAction("Index");
        }
    }
}
