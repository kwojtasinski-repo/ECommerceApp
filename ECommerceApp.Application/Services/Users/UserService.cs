using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.User;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Services.Users
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMapper _mapper;

        public UserService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IMapper mapper)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
        }

        public async Task<IdentityResult> ChangeRoleAsync(string id, IEnumerable<string> roles)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return null;
            }
            var userRoles = await _userManager.GetRolesAsync(user);

            var rolesToAdd = new List<string>();
            var rolesToDelete = new List<string>();
            foreach (var role in userRoles)
            {
                if (roles.Any(r => r == role))
                {
                    continue;
                }

                rolesToDelete.Add(role);
            }

            foreach (var role in roles)
            {
                if (userRoles.Any(r => r == role))
                {
                    continue;
                }

                rolesToAdd.Add(role);
            }

            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToDelete);
            if (!removeResult.Succeeded)
            {
                return removeResult;
            }
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                return addResult;
            }

            return IdentityResult.Success;
        }

        public async Task<ListUsersVm> GetAllUsers(int pageSize, int pageNo, string searchString)
        {
            var query = _userManager.Users.Where(user => user.UserName.StartsWith(searchString));
            var users = await query.ProjectTo<UserForListVm>(_mapper.ConfigurationProvider)
                                   .Skip(pageSize * (pageNo - 1))
                                   .Take(pageSize)
                                   .ToListAsync();

            var usersList = new ListUsersVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Users = users,
                Count = await query.CountAsync()
            };

            return usersList;
        }

        public async Task<NewUserVm> GetUserById(string id)
        {
            var user = _userManager.FindByIdAsync(id).Result;
            var userVm = _mapper.Map<NewUserVm>(user);

            if (userVm is null)
            {
                return null;
            }

            userVm.UserRoles = await GetRolesByUser(user.Id) as List<string>;
            userVm.Roles = await GetAllRoles();
            return userVm;
        }

        public async Task<IList<string>> GetRolesByUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id)
                ?? throw new BusinessException($"User with id '{id}' was not found", "userNotFound", new Dictionary<string, string> { { "id", id } });
            return (await _userManager.GetRolesAsync(user)) ?? new List<string>();
        }

        public async Task<List<RoleVm>> GetAllRoles()
        {
            return _mapper.Map<List<RoleVm>>(await _roleManager.Roles?.ToListAsync());
        }

        public async Task RemoveRoleFromUser(string id, string role)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return;
            }

            await _userManager.RemoveFromRoleAsync(user, role);
        }

        public async Task<IdentityResult> DeleteUserAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return null;
            }
            return await _userManager.DeleteAsync(user);
        }

        public async Task<IdentityResult> EditUser(NewUserVm userToEdit)
        {
            if (userToEdit is null)
            {
                throw new BusinessException($"{typeof(NewUserVm).Name} cannot be null");
            }

            var currentUser = await _userManager.FindByIdAsync(userToEdit.Id);
            if (currentUser is null)
            {
                return null;
            }
            if (currentUser.Email != userToEdit.Email)
            {
                currentUser.Email = userToEdit.Email;
                currentUser.UserName = userToEdit.Email; // login is same as email
            }
            currentUser.EmailConfirmed = userToEdit.EmailConfirmed;
            return await _userManager.UpdateAsync(currentUser);
        }

        public async Task<IdentityResult> AddUser(NewUserToAddVm newUser)
        {
            if (newUser is null)
            {
                throw new BusinessException($"{typeof(NewUserToAddVm).Name} cannot be null");
            }

            var user = new ApplicationUser()
            {
                UserName = newUser.UserName,
                Email = newUser.Email,
                EmailConfirmed = newUser.EmailConfirmed
            };
            var result = await _userManager.CreateAsync(user, newUser.Password);
            if (!result.Succeeded)
            {
                return result;
            }
            result = await AddRolesToUserAsync(user, newUser.UserRoles);
            newUser.Id = user.Id;
            return result;
        }

        private async Task<IdentityResult> AddRolesToUserAsync(ApplicationUser user, IEnumerable<string> roles)
        {
            IdentityResult result;
            roles = await RemoveDuplicateRoles(user, roles);
            result = await _userManager.AddToRolesAsync(user, roles);
            return result;
        }

        private async Task<List<string>> RemoveDuplicateRoles(ApplicationUser user, IEnumerable<string> roles)
        {
            var userRoles = (await _userManager.GetRolesAsync(user)).ToList();
            var rolesToAdd = roles.Where(r => !userRoles.Contains(r)).ToList();
            return rolesToAdd;
        }

        public async Task<IdentityResult> ChangeUserPassword(NewUserVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(NewUserVm).Name} cannot be null");
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return null;
            }
            await _userManager.RemovePasswordAsync(user);
            return await _userManager.AddPasswordAsync(user, model.PasswordToChange);
        }
    }
}
