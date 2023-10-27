using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
    public class AddressController : Controller
    {
        private readonly IAddressService _addressService;

        public AddressController(IAddressService addressService)
        {
            _addressService = addressService;
        }

        [HttpGet]
        public IActionResult AddAddress(int id)
        {
            ViewBag.CustomerId = id;
            return View();
        }

        [HttpPost]
        public IActionResult AddAddress(AddressVm address)
        {
            _addressService.AddAddress(address);
            return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = address.CustomerId });
        }

        [HttpGet]
        public IActionResult EditAddress(int id)
        {
            var address = _addressService.GetAddress(id);
            return View(address);
        }

        [HttpPost]
        public IActionResult EditAddress(AddressVm model)
        {
            _addressService.UpdateAddress(model);
            return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = model.CustomerId });
        }

        public IActionResult ViewAddress(int id)
        {
            var address = _addressService.GetAddress(id);
            return View(address);
        }

        public IActionResult DeleteAddress(int id)
        {
            _addressService.DeleteAddress(id);
            return RedirectToAction(actionName: "Index", controllerName: "Customer");
        }
    }
}
