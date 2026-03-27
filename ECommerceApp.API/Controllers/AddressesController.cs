using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/addresses")]
    public class AddressesController : BaseController
    {
        private readonly IUserProfileService _profiles;

        public AddressesController(IUserProfileService profiles)
        {
            _profiles = profiles;
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] AddAddressDto dto)
        {
            var userId = GetUserId();
            var added = await _profiles.AddAddressAsync(dto.UserProfileId, userId, dto);
            return added ? StatusCode(StatusCodes.Status201Created) : NotFound();
        }

        [HttpPut("{profileId:int}")]
        public async Task<IActionResult> Update(int profileId, [FromBody] UpdateAddressDto dto)
        {
            var userId = GetUserId();
            var updated = await _profiles.UpdateAddressAsync(profileId, userId, dto);
            return updated ? Ok() : NotFound();
        }

        [HttpDelete("{profileId:int}/{addressId:int}")]
        public async Task<IActionResult> Remove(int profileId, int addressId)
        {
            var userId = GetUserId();
            var removed = await _profiles.RemoveAddressAsync(profileId, addressId, userId);
            return removed ? NoContent() : NotFound();
        }
    }
}
