using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly IAuthenticationService _authenticationService;

        public LoginController(IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }

        [AllowAnonymous]
        [HttpPost]
        public ActionResult<SignInResponseDto> Login([FromBody] SignInDto signInDto)
        {
            return _authenticationService.SignIn(signInDto);
        }
    }
}
