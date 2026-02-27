using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Identity.IAM.ViewModels;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Identity.IAM;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Identity.IAM
{
    public class UserManagementServiceTests : BaseTest
    {
        private readonly Mock<IUserManager<ApplicationUser>> _userManager;
        private readonly Mock<RoleManager<IdentityRole>> _roleManager;

        public UserManagementServiceTests()
        {
            _userManager = new Mock<IUserManager<ApplicationUser>>();

            var roleStore = new Mock<IRoleStore<IdentityRole>>();
            _roleManager = new Mock<RoleManager<IdentityRole>>(
                roleStore.Object, null, null, null, null);
        }

        private UserManagementService CreateService()
            => new(_userManager.Object, _roleManager.Object, _mapper);

        [Fact]
        public async Task GetUserByIdAsync_NonExistentId_ShouldThrowBusinessException()
        {
            const string id = "non-existent-id";
            _userManager.Setup(um => um.FindByIdAsync(id))
                        .ReturnsAsync((ApplicationUser)null);

            var service = CreateService();
            Func<Task> act = async () => await service.GetUserByIdAsync(id);

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage($"User with id '{id}' was not found");
        }

        [Fact]
        public async Task GetUserRoleAsync_NonExistentId_ShouldThrowBusinessException()
        {
            const string userId = "missing-user";
            _userManager.Setup(um => um.FindByIdAsync(userId))
                        .ReturnsAsync((ApplicationUser)null);

            var service = CreateService();
            Func<Task> act = async () => await service.GetUserRoleAsync(userId);

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage($"User with id '{userId}' was not found");
        }

        [Fact]
        public async Task ChangeUserRoleAsync_UserNotFound_ShouldThrowBusinessException()
        {
            const string userId = "missing-user";
            _userManager.Setup(um => um.FindByIdAsync(userId))
                        .ReturnsAsync((ApplicationUser)null);

            var service = CreateService();
            Func<Task> act = async () => await service.ChangeUserRoleAsync(userId, "role-1");

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage($"User with id '{userId}' was not found");
        }

        [Fact]
        public async Task DeleteUserAsync_UserNotFound_ShouldThrowBusinessException()
        {
            const string userId = "missing-user";
            _userManager.Setup(um => um.FindByIdAsync(userId))
                        .ReturnsAsync((ApplicationUser)null);

            var service = CreateService();
            Func<Task> act = async () => await service.DeleteUserAsync(userId);

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage($"User with id '{userId}' was not found");
        }

        [Fact]
        public async Task DeleteUserAsync_ValidUser_ShouldDeleteUser()
        {
            const string userId = "user-1";
            var user = new ApplicationUser { Id = userId };
            _userManager.Setup(um => um.FindByIdAsync(userId))
                        .ReturnsAsync(user);
            _userManager.Setup(um => um.DeleteAsync(user))
                        .ReturnsAsync(IdentityResult.Success);

            var service = CreateService();
            Func<Task> act = async () => await service.DeleteUserAsync(userId);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UpdateUserAsync_NullVm_ShouldThrowBusinessException()
        {
            var service = CreateService();
            Func<Task> act = async () => await service.UpdateUserAsync(null);

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage($"{nameof(UserDetailsVm)} cannot be null");
        }

        [Fact]
        public async Task UpdateUserAsync_UserNotFound_ShouldThrowBusinessException()
        {
            var vm = new UserDetailsVm { Id = "missing-user", Email = "x@x.com", UserName = "x@x.com" };
            _userManager.Setup(um => um.FindByIdAsync(vm.Id))
                        .ReturnsAsync((ApplicationUser)null);

            var service = CreateService();
            Func<Task> act = async () => await service.UpdateUserAsync(vm);

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage($"User with id '{vm.Id}' was not found");
        }

        [Fact]
        public async Task CreateUserAsync_NullVm_ShouldThrowBusinessException()
        {
            var service = CreateService();
            Func<Task> act = async () => await service.CreateUserAsync(null);

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage($"{nameof(CreateUserVm)} cannot be null");
        }

        [Fact]
        public async Task ChangePasswordAsync_UserNotFound_ShouldThrowBusinessException()
        {
            const string userId = "missing-user";
            _userManager.Setup(um => um.FindByIdAsync(userId))
                        .ReturnsAsync((ApplicationUser)null);

            var service = CreateService();
            Func<Task> act = async () => await service.ChangePasswordAsync(userId, "NewPass1!");

            await act.Should().ThrowAsync<BusinessException>()
                     .WithMessage($"User with id '{userId}' was not found");
        }

        [Fact]
        public async Task ChangePasswordAsync_ValidUser_ShouldChangePassword()
        {
            const string userId = "user-1";
            const string newPassword = "NewPass1!";
            var user = new ApplicationUser { Id = userId };
            _userManager.Setup(um => um.FindByIdAsync(userId))
                        .ReturnsAsync(user);
            _userManager.Setup(um => um.RemovePasswordAsync(user))
                        .ReturnsAsync(IdentityResult.Success);
            _userManager.Setup(um => um.AddPasswordAsync(user, newPassword))
                        .ReturnsAsync(IdentityResult.Success);

            var service = CreateService();
            Func<Task> act = async () => await service.ChangePasswordAsync(userId, newPassword);

            await act.Should().NotThrowAsync();
        }
    }
}
