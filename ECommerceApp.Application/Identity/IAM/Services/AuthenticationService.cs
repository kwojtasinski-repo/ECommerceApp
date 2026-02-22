using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.DTOs;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Model;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Identity.IAM.Services
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

        public async Task<SignInResponseDto> SignInAsync(SignInDto dto)
        {
            var result = await _signInManager.PasswordSignInAsync(dto.Email, dto.Password, true, lockoutOnFailure: false);
            if (!result.Succeeded)
            {
                throw new BusinessException("Invalid credentials");
            }

            var user = await _userManager.FindByNameAsync(dto.Email)
                ?? throw new BusinessException("Invalid credentials");
            var roles = await _userManager.GetRolesAsync(user);
            var jwtToken = _jwtManager.IssueToken(user, roles);
            return new SignInResponseDto(jwtToken, "");
        }
    }
}
