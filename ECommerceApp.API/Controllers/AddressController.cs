using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Addresses;
using ECommerceApp.Application.ViewModels.Address;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/addresses")]
    public class AddressController : BaseController
    {
        private readonly IAddressService _addressService;

        public AddressController(IAddressService addressService)
        {
            _addressService = addressService;
        }

        [HttpGet("{id:int}")]
        public ActionResult<AddressVm> GetAddress(int id)
        {
            var address = _addressService.GetAddressDetail(id);
            if (address == null)
            {
                return NotFound();
            }
            return Ok(address);
        }

        [HttpPut]
        public IActionResult EditAddress([FromBody] AddressDto model)
        {
            if (!ModelState.IsValid)
            {
                return Conflict(ModelState);
            }
            return _addressService.UpdateAddress(model)
                ? Ok()
                : NotFound();
        }

        [HttpPost]
        public IActionResult AddAddress([FromBody] AddressDto model)
        {
            if (!ModelState.IsValid || (model.Id.HasValue && model.Id.Value != 0))
            {
                return Conflict(ModelState);
            }

            var id = _addressService.AddAddress(model);

            return Ok(id);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpDelete("{id:int}")]
        public IActionResult DeleteAddress(int id)
        {
            return _addressService.DeleteAddress(id)
                ? Ok()
                : NotFound();
        }
    }
}
