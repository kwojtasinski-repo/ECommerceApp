using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Application.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ECommerceApp.Web.Controllers
{
    public class AddressController : Controller
    {
        private readonly IAddressService _addressService;

        public AddressController(IAddressService addressService)
        {
            _addressService = addressService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult AddAddress(int id)
        {
            ViewBag.CustomerId = id;
            return View();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddAddress(AddressVm address)
        {
            _addressService.AddAddress(address);
            return RedirectToAction(actionName: "Index", controllerName: "Customer");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult EditAddress(int id)
        {
            var address = _addressService.GetAddress(id);
            return View(address);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult EditAddress(AddressVm model)
        {
            _addressService.UpdateAddress(model);
            return RedirectToAction(actionName: "Index", controllerName: "Customer");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ViewAddress(int id)
        {
            var address = _addressService.GetAddress(id);
            return View(address);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult DeleteAddress(int id)
        {
            _addressService.DeleteAddress(id);
            return RedirectToAction(actionName: "Index", controllerName: "Customer");
        }
    }
}
