using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class LoginController : BaseController
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
