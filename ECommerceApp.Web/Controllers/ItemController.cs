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
        private readonly ITagService _tagService;
        private readonly ICurrencyService _currencyService;

        public ItemController(IItemService itemService, IImageService imageService, IBrandService brandService, ITypeService typeService, ITagService tagService, ICurrencyService currencyService)
        {
            _itemService = itemService;
            _imageService = imageService;
            _brandService = brandService;
            _typeService = typeService;
            _tagService = tagService;
            _currencyService = currencyService;
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
            ViewBag.ItemTags = _tagService.GetTags(t => true);
            var currencies = _currencyService.GetAll(cr => true);
            ViewBag.Currencies = currencies;
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
        public IActionResult EditItem(int id)
        {
            var item = _itemService.GetItemById(id);
            if (item is null)
            {
                return NotFound();
            }
            ViewBag.ItemBrands = _brandService.GetAllBrands(b => true);
            ViewBag.ItemTypes = _typeService.GetTypes(t => true);
            ViewBag.ItemTags = _tagService.GetTags(t => true);
            var currencies = _currencyService.GetAll(cr => true);
            ViewBag.Currencies = currencies;
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
            if (item is null)
            {
                return NotFound();
            }
            item.Images = _imageService.GetImagesByItemId(id);
            return View(item);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteItem(int id)
        {
            _itemService.DeleteItem(id);
            return RedirectToAction("Index");
        }
    }
}
