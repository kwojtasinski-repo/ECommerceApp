using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.User;
using ECommerceApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class UserServiceTests : BaseTest<IUserService>
    {
        [Fact]
        public void given_users_in_db_should_retrun_users()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var users = _service.GetAllUsers(pageSize, pageNo, searchString);

            users.Count.ShouldBeGreaterThan(0);
            users.Users.Count.ShouldBeGreaterThan(0);
            users.SearchString.ShouldBe(searchString);
            users.CurrentPage.ShouldBe(pageNo);
            users.PageSize.ShouldBe(pageSize);
        }

        [Fact]
        public void given_invalid_search_string_should_retrun_empty_users()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "sadgsgyey654745u7hrf";

            var users = _service.GetAllUsers(pageSize, pageNo, searchString);

            users.Count.ShouldBe(0);
            users.Users.Count.ShouldBe(0);
            users.SearchString.ShouldBe(searchString);
            users.CurrentPage.ShouldBe(pageNo);
            users.PageSize.ShouldBe(pageSize);
        }

        [Fact]
        public void given_valid_id_should_return_user()
        {
            var id = "e4fc1feb-7d08-4207-bd52-3f3464a01564";
            var email = "test2@test2";

            var user = _service.GetUserById(id);

            user.ShouldNotBeNull();
            user.Email.ShouldBe(email);
        }

        [Fact]
        public void given_invalid_id_should_return_user()
        {
            var id = "";

            var user = _service.GetUserById(id);

            user.ShouldBeNull();
        }

        [Fact]
        public async Task given_valid_id_and_role_should_add_role_to_user()
        {
            var id = "e4fc1feb-7d08-4207-bd52-3f3464a01564";
            var role = "Administrator";

            await _service.ChangeRoleAsync(id, new List<string> { role });

            var user = _service.GetUserById(id);
            user.UserRoles.Where(r => r == role).FirstOrDefault().ShouldNotBeNull();
        }

        [Fact]
        public void when_get_all_roles_should_return_list()
        {
            var roles = _service.GetAllRoles().ToList();

            roles.ShouldNotBeEmpty();
        }

        [Fact]
        public void given_valid_id_when_get_all_roles_by_user_should_return_list()
        {
            var id = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";

            var roles = _service.GetRolesByUser(id).ToList();

            roles.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task given_valid_id_and_role_should_remove_role_from_user()
        {
            var id = "e4fc1feb-7d08-4207-bd52-3f3464a01564";
            var role = "Administrator";
            await _service.ChangeRoleAsync(id, new List<string> { role });

            _service.RemoveRoleFromUser(id, role);

            var user = _service.GetUserById(id);
            user.UserRoles.Where(r => r.Contains(role)).FirstOrDefault().ShouldBeNull();
        }

        [Fact]
        public async Task given_valid_user_should_add()
        {
            var user = CreateUser();

            var result = await _service.AddUser(user);

            var users = _service.GetAllUsers(20, 1, "");
            result.Succeeded.ShouldBeTrue();
            users.Users.Where(u => u.UserName == user.UserName).FirstOrDefault().ShouldNotBeNull();
        }

        [Fact]
        public async Task given_invalid_user_password_shouldnt_add()
        {
            var user = CreateUser();
            user.Password = "abc";

            var result = await _service.AddUser(user);

            var users = _service.GetAllUsers(20, 1, "");
            result.Succeeded.ShouldBeFalse();
            result.Errors.Where(e => e.Code.Contains("PasswordTooShort")).FirstOrDefault().ShouldNotBeNull();
            users.Users.Where(u => u.UserName == user.UserName).FirstOrDefault().ShouldBeNull();
        }

        [Fact]
        public async Task given_valid_password_should_change()
        {
            var user = CreateUser();
            await _service.AddUser(user);
        }

        [Fact]
        public async Task given_valid_user_should_update()
        {
            var user = CreateSampleUser();
            user.EmailConfirmed = false;

            var result = await _service.EditUser(user);

            result.Succeeded.ShouldBeTrue();
            var userUpdated = _service.GetUserById(user.Id);
            userUpdated.EmailConfirmed.ShouldBeFalse();
        }

        private NewUserToAddVm CreateUser()
        {
            var user = new NewUserToAddVm
            {
                Email = "testtest@testtest",
                EmailConfirmed = true,
                Password = "Test123456789!@",
                UserName = "testtest@testtest"
            };
            return user;
        }

        private NewUserVm CreateSampleUser()
        {
            var user = new NewUserVm
            {
                Id = "e4fc1feb-7d08-4207-bd52-3f3464a01564",
                Email = "test@test",
                UserName = "test@test",
                EmailConfirmed = true
            };
            return user;
        }
    }
}
