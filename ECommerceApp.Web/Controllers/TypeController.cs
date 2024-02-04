using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Type;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ECommerceApp.Web.Controllers
{
    [Authorize]
    public class TypeController : BaseController
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

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult AddType()
        {
            return View(new TypeVm { Type = new TypeDto() });
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult AddType(TypeVm model)
        {
            try
            {
                _typeService.AddType(model.Type);
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                var errorModel = BuildErrorModel(exception.ErrorCode, exception.Arguments);
                return RedirectToAction("Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult EditType(int id)
        {
            var type = _typeService.GetTypeById(id);
            if (type is null)
            {
                var errorModel = BuildErrorModel("typeNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new TypeVm() { Type = new TypeDto() });
            }
            return View(new TypeVm { Type = type });
        }
        
        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult EditType(TypeVm model)
        {
            try
            {
                if (!_typeService.UpdateType(model.Type))
                {
                    var errorModel = BuildErrorModel("typeNotFound", new Dictionary<string, string> { { "id", $"{model.Type.Id}" } });
                    return RedirectToAction("Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
                }
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                var errorModel = BuildErrorModel(exception.ErrorCode, exception.Arguments);
                return RedirectToAction("Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet] 
        public IActionResult ViewType(int id)
        {
            var item = _typeService.GetTypeDetails(id);
            if (item is null)
            {
                var errorModel = BuildErrorModel("typeNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new TypeDetailsVm());
            }
            return View(item);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        public IActionResult DeleteType(int id)
        {
            try
            {
                return _typeService.DeleteType(id)
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
