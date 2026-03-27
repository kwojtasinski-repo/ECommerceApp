using ECommerceApp.Application.Identity.IAM.DTOs;
using ECommerceApp.Application.Identity.IAM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/auth")]
    public class AuthController : BaseController
    {
        private readonly IAuthenticationService _auth;

        public AuthController(IAuthenticationService auth)
        {
            _auth = auth;
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(SignInResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SignInResponseDto>> Refresh([FromBody] RefreshTokenDto dto)
        {
            var result = await _auth.RefreshAsync(dto.RefreshToken);
            return Ok(result);
        }

        [HttpPost("revoke")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Revoke([FromBody] RefreshTokenDto dto)
        {
            await _auth.RevokeAsync(dto.RefreshToken);
            return NoContent();
        }
    }
}
