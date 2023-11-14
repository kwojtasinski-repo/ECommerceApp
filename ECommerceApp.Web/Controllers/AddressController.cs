using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Addresses;
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
            return View(new AddressVm { CustomerId = id });
        }

        [HttpPost]
        public IActionResult AddAddress(AddressVm addressVm)
        {
            try
            {
                _addressService.AddAddress(addressVm.Address);
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = addressVm.Address.CustomerId });
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "AddAddress", controllerName: "Address", new { Id = addressVm.Address.CustomerId, Error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult EditAddress(int id)
        {
            var address = _addressService.GetAddress(id);
            return address is null
                ? NotFound()
                : View(address);
        }

        [HttpPost]
        public IActionResult EditAddress(AddressVm model)
        {
            try
            { 
                _addressService.UpdateAddress(model.Address);
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = model.Address.CustomerId });
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "EditAddress", controllerName: "Address", new { Id = model.Address.CustomerId, Error = ex.Message });
            }
        }

        public IActionResult ViewAddress(int id)
        {
            var address = _addressService.GetAddress(id);
            return address is null
                ? NotFound()
                : View(address);
        }

        public IActionResult DeleteAddress(int id)
        {
            return _addressService.DeleteAddress(id)
                ? Json(new { Success = true })
                : NotFound();
        }
    }
}
