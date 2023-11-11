using ECommerceApp.Application.ViewModels.User;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Services.Users
{
    public interface IUserService
    {
        ListUsersVm GetAllUsers(int pageSize, int pageNo, string searchString);
        NewUserVm GetUserById(string id);
        Task<IdentityResult> ChangeRoleAsync(string id, IEnumerable<string> role);
        IQueryable<string> GetRolesByUser(string id);
        IQueryable<RoleVm> GetAllRoles();
        void RemoveRoleFromUser(string id, string role);
        Task<IdentityResult> DeleteUserAsync(string id);
        Task<IdentityResult> EditUser(NewUserVm userToEdit);
        Task<IdentityResult> AddUser(NewUserToAddVm newUser);
        Task ChangeUserPassword(NewUserVm model);
    }
}
