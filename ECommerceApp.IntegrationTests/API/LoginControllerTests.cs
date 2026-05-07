using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.DTOs;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Identity.IAM.ViewModels;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.IntegrationTests.API
{
    public class LoginControllerTests : BcBaseTest<IAuthenticationService>
    {
        public LoginControllerTests(ITestOutputHelper output) : base(output) { }

        private const string Password = "Test@1234!";

        private async Task<string> CreateTestUserAsync()
        {
            var email = $"login{Guid.NewGuid():N}@test.com";
            var userMgmt = GetRequiredService<IUserManagementService>();
            await userMgmt.CreateUserAsync(new CreateUserVm
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Password = Password,
                UserRole = "User"
            });
            return email;
        }

        [Fact]
        public async Task given_valid_credentials_should_return_token()
        {
            var email = await CreateTestUserAsync();

            var result = await _service.SignInAsync(new SignInDto(email, Password));

            result.ShouldNotBeNull();
            result.AccessToken.ShouldNotBeNullOrWhiteSpace();
            result.AccessToken.Length.ShouldBeGreaterThan(1);
            result.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task given_invalid_credentials_should_throw_business_exception()
        {
            await Should.ThrowAsync<BusinessException>(
                () => _service.SignInAsync(new SignInDto("nonexistent@test.com", "WrongPassword1!")));
        }
    }
}

