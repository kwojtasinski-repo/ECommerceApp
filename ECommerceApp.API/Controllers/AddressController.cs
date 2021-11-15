using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Application.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Route("api/addresses")]
    [ApiController]
    public class AddressController : ControllerBase
    {
        private readonly IAddressService _addressService;

        public AddressController(ICustomerService customerService, IAddressService addressService)
        {
            _addressService = addressService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("{id}")]
        public ActionResult<AddressVm> GetAddress(int id)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var address = _addressService.GetAddressDetail(id, userId);
            if (address == null)
            {
                return NotFound();
            }
            return Ok(address);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPut]
        public IActionResult EditAddress([FromBody] AddressVm model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var modelExists = _addressService.AddressExists(model.Id, userId);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _addressService.UpdateAddress(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddAddress([FromBody] AddressVm model)
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }

            var id = _addressService.AddAddress(model, userId);

            if (id == 0)
            {
                return NotFound();
            }

            return Ok();
        }
    }
}
