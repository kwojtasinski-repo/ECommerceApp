using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.DTOs;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Model;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Identity.IAM
{
    public class AuthenticationServiceTests
    {
        private readonly Mock<ISignInManager<ApplicationUser>> _signInManager;
        private readonly Mock<IJwtManager> _jwtManager;
        private readonly Mock<IUserManager<ApplicationUser>> _userManager;

        public AuthenticationServiceTests()
        {
            _signInManager = new Mock<ISignInManager<ApplicationUser>>();
            _jwtManager = new Mock<IJwtManager>();
            _userManager = new Mock<IUserManager<ApplicationUser>>();
        }

        private AuthenticationService CreateService()
            => new(_signInManager.Object, _jwtManager.Object, _userManager.Object);

        [Fact]
        public async Task SignInAsync_InvalidCredentials_ShouldThrowBusinessException()
        {
            var dto = new SignInDto("test@test.com", "wrongPassword");
            _signInManager.Setup(s => s.PasswordSignInAsync(dto.Email, dto.Password, true, false))
                          .ReturnsAsync(SignInResult.Failed);

            var service = CreateService();
            Func<Task> act = async () => await service.SignInAsync(dto);

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage("Invalid credentials");
        }

        [Fact]
        public async Task SignInAsync_UserNotFoundAfterSignIn_ShouldThrowBusinessException()
        {
            var dto = new SignInDto("ghost@test.com", "Password1!");
            _signInManager.Setup(s => s.PasswordSignInAsync(dto.Email, dto.Password, true, false))
                          .ReturnsAsync(SignInResult.Success);
            _userManager.Setup(u => u.FindByNameAsync(dto.Email))
                        .ReturnsAsync((ApplicationUser)null);

            var service = CreateService();
            Func<Task> act = async () => await service.SignInAsync(dto);

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage("Invalid credentials");
        }

        [Fact]
        public async Task SignInAsync_ValidCredentials_ShouldReturnTokenResponse()
        {
            var dto = new SignInDto("user@test.com", "Password1!");
            var user = new ApplicationUser { Id = "user-1", Email = dto.Email };
            var roles = new List<string> { "User" };
            const string expectedToken = "jwt-token-value";

            _signInManager.Setup(s => s.PasswordSignInAsync(dto.Email, dto.Password, true, false))
                          .ReturnsAsync(SignInResult.Success);
            _userManager.Setup(u => u.FindByNameAsync(dto.Email))
                        .ReturnsAsync(user);
            _userManager.Setup(u => u.GetRolesAsync(user))
                        .ReturnsAsync(roles);
            _jwtManager.Setup(j => j.IssueToken(user, roles))
                       .Returns(expectedToken);

            var service = CreateService();
            var result = await service.SignInAsync(dto);

            result.Should().NotBeNull();
            result.AccessToken.Should().Be(expectedToken);
            result.RefreshToken.Should().BeEmpty();
        }
    }
}
