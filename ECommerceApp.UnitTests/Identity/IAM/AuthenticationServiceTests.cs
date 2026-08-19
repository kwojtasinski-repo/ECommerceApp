using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.DTOs;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Identity.IAM;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Identity.IAM
{
    public class AuthenticationServiceTests
    {
        private readonly Mock<ISignInManager<ApplicationUser>> _signInManager;
        private readonly Mock<IJwtManager> _jwtManager;
        private readonly Mock<IUserManager<ApplicationUser>> _userManager;
        private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository;
        private readonly Mock<IRefreshTokenOptions> _refreshTokenOptions;

        public AuthenticationServiceTests()
        {
            _signInManager = new Mock<ISignInManager<ApplicationUser>>();
            _jwtManager = new Mock<IJwtManager>();
            _userManager = new Mock<IUserManager<ApplicationUser>>();
            _refreshTokenRepository = new Mock<IRefreshTokenRepository>();
            _refreshTokenOptions = new Mock<IRefreshTokenOptions>();
            _refreshTokenOptions.Setup(o => o.RefreshTokenTtlDays).Returns(7);
        }

        private AuthenticationService CreateService()
            => new(_signInManager.Object, _jwtManager.Object, _userManager.Object,
                   _refreshTokenRepository.Object, _refreshTokenOptions.Object);

        private void SetupSignInResult(string email, string password, SignInResult result)
        {
            _signInManager.Setup(s => s.PasswordSignInAsync(email, password, true, false))
                .ReturnsAsync(result);
        }

        private void SetupUserLookup(string email, ApplicationUser user)
        {
            _userManager.Setup(u => u.FindByNameAsync(email)).ReturnsAsync(user);
        }

        private void SetupRefreshTokenLookup(string token, RefreshToken refreshToken)
        {
            _refreshTokenRepository.Setup(r => r.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
                .ReturnsAsync(refreshToken);
        }

        [Fact]
        public async Task SignInAsync_InvalidCredentials_ShouldThrowBusinessException()
        {
            var dto = new SignInDto("test@test.com", "wrongPassword");
            SetupSignInResult(dto.Email, dto.Password, SignInResult.Failed);

            var service = CreateService();
            Func<Task> act = async () => await service.SignInAsync(dto);

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage("Invalid credentials");
        }

        [Fact]
        public async Task SignInAsync_UserNotFoundAfterSignIn_ShouldThrowBusinessException()
        {
            var dto = new SignInDto("ghost@test.com", "Password1!");
            SetupSignInResult(dto.Email, dto.Password, SignInResult.Success);
            SetupUserLookup(dto.Email, null);

            var service = CreateService();
            Func<Task> act = async () => await service.SignInAsync(dto);

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage("Invalid credentials");
        }

        [Fact]
        public async Task SignInAsync_ValidCredentials_ShouldReturnTokenResponseWithRefreshToken()
        {
            var dto = new SignInDto("user@test.com", "Password1!");
            var user = new ApplicationUser { Id = "user-1", Email = dto.Email };
            var roles = new List<string> { "User" };
            const string expectedToken = "jwt-token-value";
            const string expectedJti = "test-jti-value";

            SetupSignInResult(dto.Email, dto.Password, SignInResult.Success);
            SetupUserLookup(dto.Email, user);
            _userManager.Setup(u => u.GetRolesAsync(user))
                        .ReturnsAsync(roles);
            _userManager.Setup(u => u.GetClaimsAsync(user))
                        .ReturnsAsync(new List<Claim>());
            _jwtManager.Setup(j => j.IssueToken(user.Id, user.Email, roles, It.IsAny<IEnumerable<Claim>>()))
                       .Returns(new IssuedJwt(expectedToken, expectedJti));

            var service = CreateService();
            var result = await service.SignInAsync(dto);

            result.Should().NotBeNull();
            result.AccessToken.Should().Be(expectedToken);
            result.RefreshToken.Should().NotBeNullOrEmpty();
            _refreshTokenRepository.Verify(r => r.AddAsync(
                It.Is<RefreshToken>(rt => rt.UserId == user.Id && rt.JwtId == expectedJti),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_ValidToken_ShouldReturnNewTokenPairAndRevokeOld()
        {
            var user = new ApplicationUser { Id = "user-1", Email = "user@test.com" };
            var roles = new List<string> { "User" };
            var oldToken = RefreshToken.Create(user.Id, "old-refresh-token", "old-jti", DateTime.UtcNow.AddDays(7));
            const string newAccessToken = "new-jwt";
            const string newJti = "new-jti";

            SetupRefreshTokenLookup("old-refresh-token", oldToken);
            _userManager.Setup(u => u.FindByIdAsync(user.Id)).ReturnsAsync(user);
            _userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(roles);
            _userManager.Setup(u => u.GetClaimsAsync(user)).ReturnsAsync(new List<Claim>());
            _jwtManager.Setup(j => j.IssueToken(user.Id, user.Email, roles, It.IsAny<IEnumerable<Claim>>()))
                       .Returns(new IssuedJwt(newAccessToken, newJti));

            var service = CreateService();
            var result = await service.RefreshAsync("old-refresh-token");

            result.AccessToken.Should().Be(newAccessToken);
            result.RefreshToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBe("old-refresh-token");
            oldToken.IsRevoked.Should().BeTrue();
            _refreshTokenRepository.Verify(r => r.AddAsync(
                It.Is<RefreshToken>(rt => rt.JwtId == newJti),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_ExpiredToken_ShouldThrowBusinessException()
        {
            var expiredToken = RefreshToken.Create("user-1", "expired-token", "jti", DateTime.UtcNow.AddDays(-1));

            SetupRefreshTokenLookup("expired-token", expiredToken);

            var service = CreateService();
            Func<Task> act = async () => await service.RefreshAsync("expired-token");

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage("Refresh token has expired");
        }

        [Fact]
        public async Task RefreshAsync_RevokedToken_ShouldRevokeAllAndThrow()
        {
            var revokedToken = RefreshToken.Create("user-1", "revoked-token", "jti", DateTime.UtcNow.AddDays(7));
            revokedToken.Revoke();

            SetupRefreshTokenLookup("revoked-token", revokedToken);

            var service = CreateService();
            Func<Task> act = async () => await service.RefreshAsync("revoked-token");

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage("*theft detected*");
            _refreshTokenRepository.Verify(r => r.RevokeAllForUserAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_InvalidToken_ShouldThrowBusinessException()
        {
            SetupRefreshTokenLookup("nonexistent", null);

            var service = CreateService();
            Func<Task> act = async () => await service.RefreshAsync("nonexistent");

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage("Invalid refresh token");
        }

        [Fact]
        public async Task RevokeAsync_ValidToken_ShouldMarkAsRevoked()
        {
            var token = RefreshToken.Create("user-1", "active-token", "jti", DateTime.UtcNow.AddDays(7));

            SetupRefreshTokenLookup("active-token", token);

            var service = CreateService();
            await service.RevokeAsync("active-token");

            token.IsRevoked.Should().BeTrue();
            _refreshTokenRepository.Verify(r => r.UpdateAsync(token, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RevokeAsync_InvalidToken_ShouldThrowBusinessException()
        {
            SetupRefreshTokenLookup("nonexistent", null);

            var service = CreateService();
            Func<Task> act = async () => await service.RevokeAsync("nonexistent");

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage("Invalid refresh token");
        }
    }
}
