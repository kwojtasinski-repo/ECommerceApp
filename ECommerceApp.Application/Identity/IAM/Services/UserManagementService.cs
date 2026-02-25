using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.ViewModels;
using ECommerceApp.Domain.Identity.IAM;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Identity.IAM.Services
{
    internal sealed class UserManagementService : IUserManagementService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMapper _mapper;

        public UserManagementService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IMapper mapper)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
        }

        public async Task<UserListVm> GetUsersAsync(int pageSize, int pageNo, string searchString)
        {
            var query = _userManager.Users.Where(u => u.UserName.StartsWith(searchString));
            var users = await query
                .ProjectTo<UserForListVm>(_mapper.ConfigurationProvider)
                .Skip(pageSize * (pageNo - 1))
                .Take(pageSize)
                .ToListAsync();
            return new UserListVm
            {
                Users = users,
                Count = await query.CountAsync(),
                CurrentPage = pageNo,
                PageSize = pageSize,
                SearchString = searchString
            };
        }

        public async Task<UserDetailsVm> GetUserByIdAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id)
                ?? throw new BusinessException($"User with id '{id}' was not found",
                    ErrorCode.Create("userNotFound", ErrorParameter.Create("id", id)));
            var vm = _mapper.Map<UserDetailsVm>(user);
            vm.UserRole = await GetUserRoleAsync(id);
            vm.AvailableRoles = await GetRolesAsync();
            return vm;
        }

        public async Task<string> GetUserRoleAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new BusinessException($"User with id '{userId}' was not found",
                    ErrorCode.Create("userNotFound", ErrorParameter.Create("id", userId)));
            return ((await _userManager.GetRolesAsync(user)) ?? new List<string>()).FirstOrDefault();
        }

        public async Task<IReadOnlyList<RoleVm>> GetRolesAsync()
        {
            return _mapper.Map<List<RoleVm>>(await _roleManager.Roles.ToListAsync());
        }

        public async Task ChangeUserRoleAsync(string userId, string roleId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new BusinessException($"User with id '{userId}' was not found",
                    ErrorCode.Create("userNotFound", ErrorParameter.Create("id", userId)));
            var roles = await GetRolesAsync();
            if (!roles.Any(r => r.Id == roleId))
            {
                throw new BusinessException($"Role with id '{roleId}' was not found",
                    ErrorCode.Create("roleNotFound", ErrorParameter.Create("id", roleId)));
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                throw new BusinessException(string.Join(", ", removeResult.Errors.Select(e => e.Description)));
            }

            var addResult = await _userManager.AddToRolesAsync(user, new[] { roleId });
            if (!addResult.Succeeded)
            {
                throw new BusinessException(string.Join(", ", addResult.Errors.Select(e => e.Description)));
            }
        }

        public async Task RemoveRoleFromUserAsync(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return;
            }
            await _userManager.RemoveFromRoleAsync(user, role);
        }

        public async Task DeleteUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new BusinessException($"User with id '{userId}' was not found",
                    ErrorCode.Create("userNotFound", ErrorParameter.Create("id", userId)));
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                throw new BusinessException(string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        public async Task UpdateUserAsync(UserDetailsVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{nameof(UserDetailsVm)} cannot be null");
            }

            var user = await _userManager.FindByIdAsync(vm.Id)
                ?? throw new BusinessException($"User with id '{vm.Id}' was not found",
                    ErrorCode.Create("userNotFound", ErrorParameter.Create("id", vm.Id)));
            if (user.Email != vm.Email)
            {
                user.Email = vm.Email;
                user.UserName = vm.Email;
            }
            user.EmailConfirmed = vm.EmailConfirmed;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new BusinessException(string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        public async Task CreateUserAsync(CreateUserVm vm)
        {
            if (vm is null)
            {
                throw new BusinessException($"{nameof(CreateUserVm)} cannot be null");
            }

            var user = new ApplicationUser
            {
                UserName = vm.UserName,
                Email = vm.Email,
                EmailConfirmed = vm.EmailConfirmed
            };
            var createResult = await _userManager.CreateAsync(user, vm.Password);
            if (!createResult.Succeeded)
            {
                throw new BusinessException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }

            var addRoleResult = await _userManager.AddToRolesAsync(user, new[] { vm.UserRole });
            if (!addRoleResult.Succeeded)
            {
                throw new BusinessException(string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
            }
        }

        public async Task ChangePasswordAsync(string userId, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new BusinessException($"User with id '{userId}' was not found",
                    ErrorCode.Create("userNotFound", ErrorParameter.Create("id", userId)));
            await _userManager.RemovePasswordAsync(user);
            var result = await _userManager.AddPasswordAsync(user, newPassword);
            if (!result.Succeeded)
            {
                throw new BusinessException(string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
