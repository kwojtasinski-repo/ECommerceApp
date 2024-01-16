using ECommerceApp.Application.Services.Brands;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Infrastructure.Permissions;
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

        public ItemController(IItemService itemService, IImageService imageService, IBrandService brandService, ITypeService typeService, ITagService tagService)
        {
            _itemService = itemService;
            _imageService = imageService;
            _brandService = brandService;
            _typeService = typeService;
            _tagService = tagService;
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

            searchString ??= string.Empty;
            var model = _itemService.GetAllItemsForList(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult AddItem()
        {
            ViewBag.ItemBrands = _brandService.GetAllBrands(b => true);
            ViewBag.ItemTypes = _typeService.GetTypes(t => true);
            ViewBag.ItemTags = _tagService.GetTags(t => true);
            return View(new NewItemVm());
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult AddItem(NewItemVm model)
        {
            var id = _itemService.AddItem(model);
            return RedirectToAction("EditItem", new { id });
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
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
            item.Images = _imageService.GetImagesByItemId(id);
            return View(item);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult EditItem(NewItemVm model)
        {
            _itemService.UpdateItem(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult ShowItemConnectedWithTags()
        {
            var tag = _itemService.GetAllItemsWithTags(20, 1, "");
            return View(tag);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
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
                return NotFound();
            }
            item.Images = _imageService.GetImagesByItemId(id);
            return View(item);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        public IActionResult DeleteItem(int id)
        {
            _itemService.DeleteItem(id);
            return Json("deleted");
        }
    }
}
