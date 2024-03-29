﻿using ECommerceApp.Application.Services.Users;
using ECommerceApp.Application.ViewModels.User;
using ECommerceApp.Domain.Model;
using ECommerceApp.Application.Permissions;
using ECommerceApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using System;

namespace ECommerceApp.IntegrationTests.Services
{
    public class UserServiceTests : BaseTest<IUserService>
    {
        [Fact]
        public async Task given_users_in_db_should_retrun_users()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var users = await _service.GetAllUsers(pageSize, pageNo, searchString);

            users.Count.ShouldBeGreaterThan(0);
            users.Users.Count.ShouldBeGreaterThan(0);
            users.SearchString.ShouldBe(searchString);
            users.CurrentPage.ShouldBe(pageNo);
            users.PageSize.ShouldBe(pageSize);
        }

        [Fact]
        public async Task given_invalid_search_string_should_retrun_empty_users()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "sadgsgyey654745u7hrf";

            var users = await _service.GetAllUsers(pageSize, pageNo, searchString);

            users.Count.ShouldBe(0);
            users.Users.Count.ShouldBe(0);
            users.SearchString.ShouldBe(searchString);
            users.CurrentPage.ShouldBe(pageNo);
            users.PageSize.ShouldBe(pageSize);
        }

        [Fact]
        public async Task given_valid_id_should_return_user()
        {
            var id = "e4fc1feb-7d08-4207-bd52-3f3464a01564";
            var email = "test2@test2";

            var user = await _service.GetUserById(id);

            user.ShouldNotBeNull();
            user.Email.ShouldBe(email);
        }

        [Fact]
        public async Task given_invalid_id_should_return_user()
        {
            var id = "";

            var user = await _service.GetUserById(id);

            user.ShouldBeNull();
        }

        [Fact]
        public async Task given_valid_id_and_role_should_add_role_to_user()
        {
            var id = "e4fc1feb-7d08-4207-bd52-3f3464a01564";
            var role = UserPermissions.Roles.Administrator;

            await _service.ChangeRoleAsync(id, role);

            var user = await _service.GetUserById(id);
            user.UserRole.ShouldBe(role);
        }

        [Fact]
        public async Task when_get_all_roles_should_return_list()
        {
            var roles = await _service.GetAllRoles();

            roles.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task given_valid_id_when_get_all_roles_by_user_should_return_list()
        {
            var id = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";

            var roles = await _service.GetRoleByUser(id);

            roles.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task given_valid_id_and_role_should_remove_role_from_user()
        {
            var id = "e4fc1feb-7d08-4207-bd52-3f3464a01564";
            var role = UserPermissions.Roles.Administrator;
            await _service.ChangeRoleAsync(id, role);

            await _service.RemoveRoleFromUser(id, role);

            var user = await _service.GetUserById(id);
            user.UserRole.ShouldBeNull();
        }

        [Fact]
        public async Task given_valid_user_should_add()
        {
            var user = CreateUser();

            var result = await _service.AddUser(user);

            var users = await _service.GetAllUsers(20, 1, "");
            result.Succeeded.ShouldBeTrue();
            users.Users.Where(u => u.UserName == user.UserName).FirstOrDefault().ShouldNotBeNull();
        }

        [Fact]
        public async Task given_invalid_user_password_shouldnt_add()
        {
            var user = CreateUser();
            user.Password = "abc";

            var result = await _service.AddUser(user);

            var users = await _service.GetAllUsers(20, 1, "");
            result.Succeeded.ShouldBeFalse();
            result.Errors.Where(e => e.Code.Contains("PasswordTooShort")).FirstOrDefault().ShouldNotBeNull();
            users.Users.Where(u => u.UserName == user.UserName).FirstOrDefault().ShouldBeNull();
        }

        [Fact]
        public async Task given_valid_password_should_change()
        {
            var user = CreateUser();
            await _service.AddUser(user);
            var newPassword = "PaSW0Rd!@1245";
            var changePasswordUser = new NewUserVm { Id = user.Id, UserName = user.UserName, Email = user.Email, EmailConfirmed = true, PasswordToChange = newPassword };

            await _service.ChangeUserPassword(changePasswordUser);

            using var scope = Services.CreateScope();
            var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = new DefaultHttpContext() { RequestServices = scope.ServiceProvider };
            var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            var result = await signInManager.PasswordSignInAsync(changePasswordUser.Email, changePasswordUser.PasswordToChange, true, lockoutOnFailure: false);
            result.Succeeded.ShouldBeTrue();
        }

        [Fact]
        public async Task given_valid_user_should_update()
        {
            var user = CreateSampleUser();
            user.EmailConfirmed = false;

            var result = await _service.EditUser(user);

            result.Succeeded.ShouldBeTrue();
            var userUpdated = await _service.GetUserById(user.Id);
            userUpdated.EmailConfirmed.ShouldBeFalse();
        }

        private static NewUserToAddVm CreateUser()
        {
            var user = new NewUserToAddVm
            {
                Email = "testtest@testtest",
                EmailConfirmed = true,
                Password = "Test123456789!@",
                UserName = "testtest@testtest",
                UserRole = "User"
            };
            return user;
        }

        private static NewUserVm CreateSampleUser()
        {
            var user = new NewUserVm
            {
                Id = "e4fc1feb-7d08-4207-bd52-3f3464a01564",
                Email = $"test{Guid.NewGuid():N}@test",
                UserName = "test@test",
                EmailConfirmed = true
            };
            return user;
        }
    }
}
