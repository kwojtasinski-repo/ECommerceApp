using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.DTOs;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Identity.IAM;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Identity.IAM.Services
{
    internal sealed class AuthenticationService : IAuthenticationService
    {
        private readonly ISignInManager<ApplicationUser> _signInManager;
        private readonly IJwtManager _jwtManager;
        private readonly IUserManager<ApplicationUser> _userManager;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly int _refreshTokenTtlDays;

        public AuthenticationService(
            ISignInManager<ApplicationUser> signInManager,
            IJwtManager jwtManager,
            IUserManager<ApplicationUser> userManager,
            IRefreshTokenRepository refreshTokenRepository,
            IRefreshTokenOptions refreshTokenOptions)
        {
            _signInManager = signInManager;
            _jwtManager = jwtManager;
            _userManager = userManager;
            _refreshTokenRepository = refreshTokenRepository;
            _refreshTokenTtlDays = refreshTokenOptions.RefreshTokenTtlDays;
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
            var userClaims = await _userManager.GetClaimsAsync(user);
            var jwt = _jwtManager.IssueToken(user.Id, user.Email, roles, userClaims);

            var refreshToken = GenerateRefreshToken();
            var entity = RefreshToken.Create(
                user.Id,
                refreshToken,
                jwt.Jti,
                DateTime.UtcNow.AddDays(_refreshTokenTtlDays));
            await _refreshTokenRepository.AddAsync(entity);

            return new SignInResponseDto(jwt.Token, refreshToken);
        }

        public async Task<SignInResponseDto> RefreshAsync(string refreshToken)
        {
            var stored = await _refreshTokenRepository.GetByTokenAsync(refreshToken)
                ?? throw new BusinessException("Invalid refresh token");

            if (stored.IsRevoked)
            {
                await _refreshTokenRepository.RevokeAllForUserAsync(stored.UserId);
                throw new BusinessException("Refresh token has been revoked — possible theft detected");
            }

            if (stored.ExpiresAt <= DateTime.UtcNow)
            {
                throw new BusinessException("Refresh token has expired");
            }

            stored.Revoke();

            var user = await _userManager.FindByIdAsync(stored.UserId)
                ?? throw new BusinessException("User not found");
            var roles = await _userManager.GetRolesAsync(user);
            var userClaims = await _userManager.GetClaimsAsync(user);
            var jwt = _jwtManager.IssueToken(user.Id, user.Email, roles, userClaims);

            var newRefreshToken = GenerateRefreshToken();
            var entity = RefreshToken.Create(
                user.Id,
                newRefreshToken,
                jwt.Jti,
                DateTime.UtcNow.AddDays(_refreshTokenTtlDays));
            await _refreshTokenRepository.AddAsync(entity);

            return new SignInResponseDto(jwt.Token, newRefreshToken);
        }

        public async Task RevokeAsync(string refreshToken)
        {
            var stored = await _refreshTokenRepository.GetByTokenAsync(refreshToken)
                ?? throw new BusinessException("Invalid refresh token");

            stored.Revoke();
            await _refreshTokenRepository.UpdateAsync(stored);
        }

        private static string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }
}
