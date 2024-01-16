using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Type;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class TypeController : Controller
    {
        private readonly ITypeService _typeService;

        public TypeController(ITypeService typeService)
        {
            _typeService = typeService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var type = _typeService.GetTypes(20, 1, "");
            return View(type);
        }

        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var type = _typeService.GetTypes(pageSize, pageNo.Value, searchString);
            return View(type);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult AddType()
        {
            return View(new TypeVm { Type = new TypeDto() });
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult AddType(TypeVm model)
        {
            _typeService.AddType(model.Type);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult EditType(int id)
        {
            var type = _typeService.GetTypeById(id);
            if (type is null)
            {
                return NotFound();
            }
            return View(new TypeVm { Type = type });
        }
        
        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult EditType(TypeVm model)
        {
            _typeService.UpdateType(model.Type);
            return RedirectToAction("Index");
        }

        [HttpGet] 
        public IActionResult ViewType(int id)
        {
            var item = _typeService.GetTypeDetails(id);
            if (item is null)
            {
                return NotFound();
            }
            return View(item);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        public IActionResult DeleteType(int id)
        {
            _typeService.DeleteType(id);
            return Json("deleted");
        }
    }
}
