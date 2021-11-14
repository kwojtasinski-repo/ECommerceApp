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

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var type = _typeService.GetTypes(pageSize, pageNo.Value, searchString);
            return View(type);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult AddType()
        {
            return View(new TypeVm());
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult AddType(TypeVm model)
        {
            var id = _typeService.AddType(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult EditType(int id)
        {
            var item = _typeService.GetTypeById(id);
            if (item is null)
            {
                return NotFound();
            }
            return View(item);
        }
        
        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult EditType(TypeVm model)
        {
            _typeService.UpdateType(model);
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteType(int id)
        {
            _typeService.DeleteType(id);
            return RedirectToAction("Index");
        }
    }
}
