using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Addresses;
using ECommerceApp.Application.ViewModels.Address;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ECommerceApp.Web.Controllers
{
    [Authorize]
    public class AddressController : BaseController
    {
        private readonly IAddressService _addressService;

        public AddressController(IAddressService addressService)
        {
            _addressService = addressService;
        }

        [HttpGet]
        public IActionResult AddAddress(int id)
        {
            return View(new AddressVm { Address = new AddressDto { CustomerId = id } });
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
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", MapExceptionAsRouteValues(ex, new Dictionary<string, object>() { { "Id", addressVm.Address.CustomerId } }));
            }
        }

        [HttpGet]
        public IActionResult EditAddress(int id)
        {
            var address = _addressService.GetAddress(id);
            if (address is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("addressNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new AddressVm { Id = id, Address = new AddressDto() });
            }
            return View(new AddressVm { Id = id, Address = address });
        }

        [HttpPost]
        public IActionResult EditAddress(AddressVm model)
        {
            try
            {
                if (!_addressService.UpdateAddress(model.Address))
                {
                    var errorModel = BuildErrorModel(ErrorCode.Create("addressNotFound", ErrorParameter.Create("id", model.Id)));
                    return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", errorModel.AsOjectRoute(new Dictionary<string, object>() { { "Id", model.Address.CustomerId } }));
                }
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = model.Address.CustomerId });
            }
            catch (BusinessException ex)
            {
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", MapExceptionAsRouteValues(ex, new Dictionary<string, object>() { { "Id", model.Address.CustomerId } }));
            }
        }

        public IActionResult ViewAddress(int id)
        {
            var address = _addressService.GetAddress(id);
            if (address is null)
            {
                HttpContext.Request.Query = BuildErrorModel(ErrorCode.Create("addressNotFound", ErrorParameter.Create("id", id))).AsQueryCollection();
                return View(new AddressVm { Id = id, Address = new AddressDto() });
            }

            return View(new AddressVm { Id = id, Address = address });
        }

        public IActionResult DeleteAddress(int id)
        {
            try
            {
                return _addressService.DeleteAddress(id)
                    ? Json(new { Success = true })
                    : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(BuildErrorModel(exception).Codes);
            }
        }
    }
}
