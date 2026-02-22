using ECommerceApp.Application.Identity.IAM.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Identity.IAM.Services
{
    public interface IUserManagementService
    {
        Task<UserListVm> GetUsersAsync(int pageSize, int pageNo, string searchString);
        Task<UserDetailsVm> GetUserByIdAsync(string id);
        Task<string> GetUserRoleAsync(string userId);
        Task<IReadOnlyList<RoleVm>> GetRolesAsync();
        Task ChangeUserRoleAsync(string userId, string roleId);
        Task RemoveRoleFromUserAsync(string userId, string role);
        Task DeleteUserAsync(string userId);
        Task UpdateUserAsync(UserDetailsVm vm);
        Task CreateUserAsync(CreateUserVm vm);
        Task ChangePasswordAsync(string userId, string newPassword);
    }
}
