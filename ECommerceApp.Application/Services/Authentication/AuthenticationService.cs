using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Model;

namespace ECommerceApp.Application.Services.Authentication
{
    internal sealed class AuthenticationService : IAuthenticationService
    {
        private readonly ISignInManager<ApplicationUser> _signInManager;
        private readonly IJwtManager _jwtManager;
        private readonly IUserManager<ApplicationUser> _userManager;

        public AuthenticationService(ISignInManager<ApplicationUser> signInManager, IJwtManager jwtManager, IUserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _jwtManager = jwtManager;
            _userManager = userManager;
        }

        public SignInResponseDto SignIn(SignInDto signInDto)
        {
            // TODO: Make this method async
            var result = _signInManager.PasswordSignInAsync(signInDto.Email, signInDto.Password, true, lockoutOnFailure: false).GetAwaiter().GetResult();
            if (!result.Succeeded)
            {
                throw new BusinessException("Invalid credentials");
            }

            var user = _userManager.FindByNameAsync(signInDto.Email).GetAwaiter().GetResult() ?? throw new BusinessException("Invalid credentials");
            var roles = _userManager.GetRolesAsync(user).GetAwaiter().GetResult();
            // TODO: Store token, add timespan for expire, create refreshToken provider
            var jwtToken = _jwtManager.IssueToken(user, roles);
            var refreshToken = "";
            return new SignInResponseDto(jwtToken, refreshToken);
        }
    }
}
