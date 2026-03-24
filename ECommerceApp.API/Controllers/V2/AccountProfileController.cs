using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/customers")]
    public class AccountProfileController : BaseController
    {
        private readonly IUserProfileService _profiles;

        public AccountProfileController(IUserProfileService profiles)
        {
            _profiles = profiles;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetUserId();
            var vm = await _profiles.GetAsync(id, userId);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserProfileDto dto)
        {
            var id = await _profiles.CreateAsync(dto);
            return StatusCode(StatusCodes.Status201Created, new { id });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserProfileDto dto)
        {
            if (dto.Id != id) return BadRequest(new { error = "Id mismatch." });
            var updated = await _profiles.UpdatePersonalInfoAsync(dto);
            return updated ? Ok() : NotFound();
        }
    }
}
