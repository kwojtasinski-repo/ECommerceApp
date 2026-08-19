using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.Services;
using ECommerceApp.Application.Identity.IAM.ViewModels;
using AwesomeAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Backoffice
{
    public class BackofficeUserServiceTests
    {
        private readonly Mock<IUserManagementService> _userManagement;

        public BackofficeUserServiceTests()
        {
            _userManagement = new Mock<IUserManagementService>();
        }

        private IBackofficeUserService CreateSut() => new BackofficeUserService(_userManagement.Object);

        private void SetupUserList(int pageSize, int pageNumber, string searchString, UserListVm source)
        {
            _userManagement.Setup(s => s.GetUsersAsync(pageSize, pageNumber, searchString))
                .ReturnsAsync(source);
        }

        private void SetupUserRole(string userId, string role)
        {
            _userManagement.Setup(s => s.GetUserRoleAsync(userId)).ReturnsAsync(role);
        }

        private void SetupUserDetails(string userId, UserDetailsVm details)
        {
            _userManagement.Setup(s => s.GetUserByIdAsync(userId)).ReturnsAsync(details);
        }

        private void SetupUserDetailsFailure(string userId)
        {
            _userManagement.Setup(s => s.GetUserByIdAsync(userId)).ThrowsAsync(new BusinessException());
        }

        // ── GetUsersAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetUsersAsync_WithResults_ReturnsMappedVmWithRoles()
        {
            // Arrange
            var source = new UserListVm
            {
                Users = new List<UserForListVm>
                {
                    new() { Id = "u1", UserName = "alice@test.com", Email = "alice@test.com" },
                    new() { Id = "u2", UserName = "bob@test.com",   Email = "bob@test.com" }
                },
                CurrentPage = 1,
                PageSize = 10,
                Count = 2,
                SearchString = "test"
            };
            SetupUserList(10, 1, "test", source);
            SetupUserRole("u1", "Administrator");
            SetupUserRole("u2", "User");

            // Act
            var result = await CreateSut().GetUsersAsync(10, 1, "test", TestContext.Current.CancellationToken);

            // Assert
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.TotalCount.Should().Be(2);
            result.SearchString.Should().Be("test");
            result.Users.Should().HaveCount(2);

            result.Users[0].Id.Should().Be("u1");
            result.Users[0].UserName.Should().Be("alice@test.com");
            result.Users[0].Roles.Should().ContainSingle("Administrator");

            result.Users[1].Id.Should().Be("u2");
            result.Users[1].Roles.Should().ContainSingle("User");
        }

        [Fact]
        public async Task GetUsersAsync_NullSearch_DelegatesToEmptyString()
        {
            // Arrange
            SetupUserList(10, 1, string.Empty, new UserListVm { Users = new List<UserForListVm>() });

            // Act
            var result = await CreateSut().GetUsersAsync(10, 1, null, TestContext.Current.CancellationToken);

            // Assert
            result.Users.Should().BeEmpty();
            _userManagement.Verify(s => s.GetUsersAsync(10, 1, string.Empty), Times.Once);
        }

        [Fact]
        public async Task GetUsersAsync_EmptyRoleString_RolesListIsEmpty()
        {
            // Arrange
            SetupUserList(10, 1, string.Empty, new UserListVm
                {
                    Users = new List<UserForListVm> { new() { Id = "u1", UserName = "x", Email = "x" } }
                });
            SetupUserRole("u1", string.Empty);

            // Act
            var result = await CreateSut().GetUsersAsync(10, 1, null, TestContext.Current.CancellationToken);

            // Assert
            result.Users[0].Roles.Should().BeEmpty();
        }

        // ── GetUserDetailAsync ────────────────────────────────────────────────

        [Fact]
        public async Task GetUserDetailAsync_ExistingUser_ReturnsMappedVm()
        {
            // Arrange
            SetupUserDetails("u1", new UserDetailsVm { Id = "u1", UserName = "alice@test.com", Email = "alice@test.com" });
            SetupUserRole("u1", "Manager");

            // Act
            var result = await CreateSut().GetUserDetailAsync("u1", TestContext.Current.CancellationToken);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("u1");
            result.UserName.Should().Be("alice@test.com");
            result.Email.Should().Be("alice@test.com");
            result.Roles.Should().ContainSingle("Manager");
        }

        [Fact]
        public async Task GetUserDetailAsync_UserNotFound_ReturnsNull()
        {
            // Arrange
            SetupUserDetailsFailure("missing");

            // Act
            var result = await CreateSut().GetUserDetailAsync("missing", TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }
    }
}
