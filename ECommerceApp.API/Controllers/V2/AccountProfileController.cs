using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/v2/profiles")]
    public class AccountProfileController : BaseController
    {
        private readonly IUserProfileService _profiles;

        public AccountProfileController(IUserProfileService profiles)
        {
            _profiles = profiles;
        }

        [HttpGet]
        [Authorize(Roles = ManagingRole)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageNo = 1,
            [FromQuery] string searchString = "")
        {
            var vm = await _profiles.GetAllAsync(pageSize, pageNo, searchString ?? "");
            return Ok(vm);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetUserId();
            var vm = await _profiles.GetAsync(id, userId);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpGet("{id:int}/details")]
        [Authorize(Roles = ManagingRole)]
        public async Task<IActionResult> GetDetails(int id)
        {
            var vm = await _profiles.GetDetailsAsync(id);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = GetUserId();
            var vm = await _profiles.GetByUserIdAsync(userId);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserProfileDto dto)
        {
            var id = await _profiles.CreateAsync(dto);
            return StatusCode(StatusCodes.Status201Created, new { id });
        }

        [HttpPut("personal")]
        public async Task<IActionResult> UpdatePersonal([FromBody] UpdateUserProfileDto dto)
        {
            var updated = await _profiles.UpdatePersonalInfoAsync(dto);
            return updated ? Ok() : NotFound();
        }

        [HttpPut("contact")]
        public async Task<IActionResult> UpdateContact([FromBody] UpdateContactInfoDto dto)
        {
            var updated = await _profiles.UpdateContactInfoAsync(dto);
            return updated ? Ok() : NotFound();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = ManagingRole)]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _profiles.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }

        // ── Addresses ────────────────────────────────────────────────────────

        [HttpPost("{profileId:int}/addresses")]
        public async Task<IActionResult> AddAddress(int profileId, [FromBody] AddAddressDto dto)
        {
            var userId = GetUserId();
            var added = await _profiles.AddAddressAsync(profileId, userId, dto);
            return added ? StatusCode(StatusCodes.Status201Created) : NotFound();
        }

        [HttpPut("{profileId:int}/addresses")]
        public async Task<IActionResult> UpdateAddress(int profileId, [FromBody] UpdateAddressDto dto)
        {
            var userId = GetUserId();
            var updated = await _profiles.UpdateAddressAsync(profileId, userId, dto);
            return updated ? Ok() : NotFound();
        }

        [HttpDelete("{profileId:int}/addresses/{addressId:int}")]
        public async Task<IActionResult> RemoveAddress(int profileId, int addressId)
        {
            var userId = GetUserId();
            var removed = await _profiles.RemoveAddressAsync(profileId, addressId, userId);
            return removed ? NoContent() : NotFound();
        }
    }
}
