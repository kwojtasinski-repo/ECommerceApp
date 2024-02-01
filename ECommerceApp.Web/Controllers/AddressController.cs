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
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = addressVm.Address.CustomerId, Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult EditAddress(int id)
        {
            var address = _addressService.GetAddress(id);
            if (address is null)
            {
                var errorModel = BuildErrorModel("addressNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
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
                    var errorModel = BuildErrorModel("addressNotFound", new Dictionary<string, string> { { "id", $"{model.Id}" } });
                    return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = model.Address.CustomerId, Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
                }
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = model.Address.CustomerId });
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction(actionName: "EditCustomer", controllerName: "Customer", new { Id = model.Address.CustomerId, Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        public IActionResult ViewAddress(int id)
        {
            var address = _addressService.GetAddress(id);
            if (address is null)
            {
                var errorModel = BuildErrorModel("addressNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
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
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }
    }
}
