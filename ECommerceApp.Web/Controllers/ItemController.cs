using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Type;
using ECommerceApp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class ItemController : Controller
    {
        private readonly IItemService _itemService;
        private readonly IBrandService _brandService;
        private readonly IImageService _imageService;
        private readonly ITypeService _typeService;
        public ItemController(IItemService itemService, IImageService imageService, IBrandService brandService, ITypeService typeService)
        {
            _itemService = itemService;
            _imageService = imageService;
            _brandService = brandService;
            _typeService = typeService;
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult AddItem()
        {
            ViewBag.ItemBrands = _brandService.GetAllBrands(b => true);
            ViewBag.ItemTypes = _typeService.GetTypes(t => true);
            ViewBag.ItemTags = _itemService.GetAllItemTagsForAddingItems().ToList();
            return View(new NewItemVm());
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult AddItem(NewItemVm model)
        {
            var id = _itemService.AddItem(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult AddItemTag()
        {
            return View(new NewTagVm());
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult AddItemTag(NewTagVm model)
        {
            var id = _itemService.AddItemTag(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult EditItem(int id)
        {
            var item = _itemService.GetItemById(id);
            ViewBag.ItemBrands = _brandService.GetAllBrands(b => true);
            ViewBag.ItemTypes = _typeService.GetTypes(t => true);
            ViewBag.ItemTags = _itemService.GetAllItemTagsForAddingItems().ToList();
            item.Images = _imageService.GetImagesByItemId(id);
            return View(item);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult EditItem(NewItemVm model)
        {
            _itemService.UpdateItem(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult EditItemTag(int id)
        {
            var tag = _itemService.GetItemTagById(id);
            return View(tag);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult EditItemTag(NewTagVm model)
        {
            _itemService.UpdateItemTag(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult ShowItemTags()
        {
            var tag = _itemService.GetAllTags(20, 1, "");
            return View(tag);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult ShowItemConnectedWithTags()
        {
            var tag = _itemService.GetAllItemsWithTags(20, 1, "");
            return View(tag);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
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
            item.Images = _imageService.GetImagesByItemId(id);
            return View(item);
        }

        [Authorize(Roles = "Administratorm, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult ViewItemTag(int id)
        {
            var item = _itemService.GetItemTagById(id);
            return View(item);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItem(int id)
        {
            _itemService.DeleteItem(id);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItemTag(int id)
        {
            _itemService.DeleteItemTag(id);
            return RedirectToAction("Index");
        }
    }
}
