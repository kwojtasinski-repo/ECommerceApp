using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.DTOs;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Identity.IAM.ViewModels;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.IntegrationTests.Identity.IAM
{
    public class RefreshTokenIntegrationTests : BcBaseTest<IAuthenticationService>
    {
        public RefreshTokenIntegrationTests(ITestOutputHelper output) : base(output) { }

        private const string UserRoleId = "User";
        private const string Password = "Test@1234!";

        private async Task<SignInResponseDto> SignInAsync(string email = null)
        {
            email ??= $"user{Guid.NewGuid():N}@test.com";
            var userMgmt = GetRequiredService<IUserManagementService>();
            await userMgmt.CreateUserAsync(new CreateUserVm
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Password = Password,
                UserRole = UserRoleId
            });
            return await _service.SignInAsync(new SignInDto(email, Password));
        }

        [Fact]
        public async Task RefreshAsync_WithValidToken_ReturnsNewTokenPair()
        {
            var initial = await SignInAsync();

            var refreshed = await _service.RefreshAsync(initial.RefreshToken);

            refreshed.ShouldNotBeNull();
            refreshed.AccessToken.ShouldNotBeNullOrWhiteSpace();
            refreshed.RefreshToken.ShouldNotBeNullOrWhiteSpace();
            refreshed.RefreshToken.ShouldNotBe(initial.RefreshToken);
        }

        [Fact]
        public async Task RefreshAsync_WithInvalidToken_ThrowsBusinessException()
        {
            await Should.ThrowAsync<BusinessException>(
                () => _service.RefreshAsync("invalid-refresh-token-value"));
        }

        [Fact]
        public async Task RefreshAsync_AfterRotation_OldTokenThrowsBusinessException()
        {
            var initial = await SignInAsync();
            await _service.RefreshAsync(initial.RefreshToken);

            // Old (rotated/revoked) token must not be accepted — theft detection fires
            await Should.ThrowAsync<BusinessException>(
                () => _service.RefreshAsync(initial.RefreshToken));
        }

        [Fact]
        public async Task RevokeAsync_ThenRefresh_ThrowsBusinessException()
        {
            var initial = await SignInAsync();
            await _service.RevokeAsync(initial.RefreshToken);

            await Should.ThrowAsync<BusinessException>(
                () => _service.RefreshAsync(initial.RefreshToken));
        }
    }
}
