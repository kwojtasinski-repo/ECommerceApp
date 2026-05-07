using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Identity.IAM.ViewModels;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.IntegrationTests.Identity.IAM
{
    public class UserManagementServiceTests : BcBaseTest<IUserManagementService>
    {
        public UserManagementServiceTests(ITestOutputHelper output) : base(output) { }

        private const string UserRoleId = "User";

        private async Task<string> CreateTestUserAsync(string email = null)
        {
            email ??= $"user{Guid.NewGuid():N}@test.com";
            await _service.CreateUserAsync(new CreateUserVm
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Password = "Test@1234!",
                UserRole = UserRoleId
            });
            var vm = await _service.GetUsersAsync(100, 1, email);
            return vm.Users[0].Id;
        }

        [Fact]
        public async Task GetUsersAsync_ReturnsMatchingUsers()
        {
            var email = $"search{Guid.NewGuid():N}@test.com";
            await CreateTestUserAsync(email);

            var result = await _service.GetUsersAsync(20, 1, email);

            result.ShouldNotBeNull();
            result.Count.ShouldBe(1);
            result.Users.Count.ShouldBe(1);
            result.Users[0].Email.ShouldBe(email);
        }

        [Fact]
        public async Task GetUsersAsync_NoMatch_ReturnsEmpty()
        {
            var result = await _service.GetUsersAsync(20, 1, "zzznomatch@nowhere.xyz");

            result.Count.ShouldBe(0);
            result.Users.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetUserByIdAsync_ValidId_ReturnsUser()
        {
            var email = $"byid{Guid.NewGuid():N}@test.com";
            var id = await CreateTestUserAsync(email);

            var user = await _service.GetUserByIdAsync(id);

            user.ShouldNotBeNull();
            user.Id.ShouldBe(id);
            user.Email.ShouldBe(email);
        }

        [Fact]
        public async Task GetUserByIdAsync_InvalidId_ThrowsBusinessException()
        {
            await Should.ThrowAsync<BusinessException>(() => _service.GetUserByIdAsync(Guid.NewGuid().ToString()));
        }

        [Fact]
        public async Task GetRolesAsync_ReturnsAllRoles()
        {
            var roles = await _service.GetRolesAsync();

            roles.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task ChangeUserRoleAsync_ValidUserAndRole_ChangesRole()
        {
            var id = await CreateTestUserAsync();

            await _service.ChangeUserRoleAsync(id, "Administrator");

            var role = await _service.GetUserRoleAsync(id);
            role.ShouldBe("Administrator");
        }

        [Fact]
        public async Task CreateUserAsync_ValidVm_CreatesUser()
        {
            var email = $"create{Guid.NewGuid():N}@test.com";

            await _service.CreateUserAsync(new CreateUserVm
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Password = "Create@1234!",
                UserRole = UserRoleId
            });

            var list = await _service.GetUsersAsync(100, 1, email);
            list.Count.ShouldBe(1);
            list.Users[0].Email.ShouldBe(email);
        }

        [Fact]
        public async Task UpdateUserAsync_ValidVm_UpdatesEmail()
        {
            var id = await CreateTestUserAsync();
            var newEmail = $"updated{Guid.NewGuid():N}@test.com";
            var vm = await _service.GetUserByIdAsync(id);
            vm.Email = newEmail;

            await _service.UpdateUserAsync(vm);

            var updated = await _service.GetUserByIdAsync(id);
            updated.Email.ShouldBe(newEmail);
        }

        [Fact]
        public async Task DeleteUserAsync_ValidId_DeletesUser()
        {
            var id = await CreateTestUserAsync();

            await _service.DeleteUserAsync(id);

            await Should.ThrowAsync<BusinessException>(() => _service.GetUserByIdAsync(id));
        }

        [Fact]
        public async Task ChangePasswordAsync_ValidPassword_ChangesPassword()
        {
            var id = await CreateTestUserAsync();

            await Should.NotThrowAsync(() => _service.ChangePasswordAsync(id, "NewPass@5678!"));
        }

        [Fact]
        public async Task ChangeUserRoleAsync_InvalidRoleId_ThrowsBusinessException()
        {
            var id = await CreateTestUserAsync();

            await Should.ThrowAsync<BusinessException>(() => _service.ChangeUserRoleAsync(id, "NonExistentRole"));
        }
    }
}

