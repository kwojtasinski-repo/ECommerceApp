using ECommerceApp.Application.ViewModels.User;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Services.Users
{
    public interface IUserService
    {
        Task<ListUsersVm> GetAllUsers(int pageSize, int pageNo, string searchString);
        Task<NewUserVm> GetUserById(string id);
        Task<IdentityResult> ChangeRoleAsync(string id, IEnumerable<string> role);
        Task<IList<string>> GetRolesByUser(string id);
        Task<List<RoleVm>> GetAllRoles();
        Task RemoveRoleFromUser(string id, string role);
        Task<IdentityResult> DeleteUserAsync(string id);
        Task<IdentityResult> EditUser(NewUserVm userToEdit);
        Task<IdentityResult> AddUser(NewUserToAddVm newUser);
        Task ChangeUserPassword(NewUserVm model);
    }
}
