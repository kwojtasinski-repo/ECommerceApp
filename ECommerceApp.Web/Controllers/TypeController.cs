﻿using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Type;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult EditType(int id)
        {
            var type = _typeService.GetTypeById(id);
            if (type is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("typeNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
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
                    var errorModel = BuildErrorModel(ErrorCode.Create("typeNotFound", ErrorParameter.Create("id", model.Type.Id)));
                    return RedirectToAction("Index", errorModel.AsOjectRoute());
                }
                return RedirectToAction("Index");
            }
            catch (BusinessException exception)
            {
                return RedirectToAction("Index", MapExceptionAsRouteValues(exception));
            }
        }

        [HttpGet] 
        public IActionResult ViewType(int id)
        {
            var item = _typeService.GetTypeDetails(id);
            if (item is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("typeNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
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
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }
    }
}
