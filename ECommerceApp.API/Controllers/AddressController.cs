using ECommerceApp.Application.Services.Addresses;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace ECommerceApp.API.Controllers
{
    [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
    [Route("api/addresses")]
    [ApiController]
    public class AddressController : ControllerBase
    {
        private readonly IAddressService _addressService;

        public AddressController(IAddressService addressService)
        {
            _addressService = addressService;
        }

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

        [HttpPut]
        public IActionResult EditAddress([FromBody] AddressVm model)
        {
            var modelExists = _addressService.AddressExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _addressService.UpdateAddress(model);
            return Ok();
        }

        [HttpPost]
        public IActionResult AddAddress([FromBody] AddressVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }

            var id = _addressService.AddAddress(model);

            return Ok(id);
        }
    }
}
