using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.User;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Services
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

        public async Task<IdentityResult> ChangeRoleAsync(string id, IEnumerable<string> role)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return null;
            }
            var userRoles = await _userManager.GetRolesAsync(user);

            // if roles are more than current roles add new roles but firstly check if there will be duplicate
            // else if new roles are less than current roles then delete all and add new roles
            if (role.ToList().Count > userRoles.Count)
            {
                return await AddRolesToUserAsync(user, role);
            }
            else
            {
                return await RemoveRolesFromUserAsync(user, role);
            }
        }

        public ListUsersVm GetAllUsers(int pageSize, int pageNo, string searchString)
        {
            var users = _userManager.Users.Where(user => user.UserName.StartsWith(searchString))
                .ProjectTo<UserForListVm>(_mapper.ConfigurationProvider).ToList();

            var usersToShow = users.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var usersList = new ListUsersVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Users = usersToShow,
                Count = users.Count
            };

            return usersList;
        }

        public NewUserVm GetUserById(string id)
        {
            var user = _userManager.FindByIdAsync(id).Result;
            var userVm = _mapper.Map<NewUserVm>(user);

            if (userVm is null)
            {
                return null;
            }

            userVm.UserRoles = GetRolesByUser(user.Id).ToList();
            userVm.Roles = GetAllRoles().ToList();
            return userVm;
        }

        public IQueryable<string> GetRolesByUser(string id)
        {
            var user = _userManager.FindByIdAsync(id).Result;
            var roles = _userManager.GetRolesAsync(user).Result.AsQueryable();
            return roles;
        }

        public IQueryable<RoleVm> GetAllRoles()
        {
            var rolesVm = _roleManager.Roles?.ProjectTo<RoleVm>(_mapper.ConfigurationProvider);
            return rolesVm;
        }

        public void RemoveRoleFromUser(string id, string role)
        {
            var user = _userManager.FindByIdAsync(id).Result;
            if (user == null)
            {
                return;
            }
            _userManager.RemoveFromRoleAsync(user, role);
        }

        public async Task<IdentityResult> DeleteUserAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return await Task.FromResult<IdentityResult>(null);
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
            CheckChangesInCurrentUser(currentUser, userToEdit); // allowed only change email and verification
            return await _userManager.UpdateAsync(currentUser);
        }

        public async Task<IdentityResult> AddUser(NewUserToAddVm newUser)
        {
            if (newUser is null)
            {
                throw new BusinessException($"{typeof(NewUserToAddVm).Name} cannot be null");
            }

            //var user = _mapper.Map<IdentityUser>(newUser);
            var user = new ApplicationUser() { UserName = newUser.UserName, Email = newUser.Email,
                                            EmailConfirmed = newUser.EmailConfirmed };
            var result = await _userManager.CreateAsync(user, newUser.Password);
            newUser.Id = user.Id;
            return result;
        }

        private void CheckChangesInCurrentUser(ApplicationUser currentUser, NewUserVm userToEdit)
        {
            if(currentUser.Email != userToEdit.Email)
            {
                currentUser.Email = userToEdit.Email;
            }
            
            if(currentUser.EmailConfirmed != userToEdit.EmailConfirmed)
            {
                currentUser.EmailConfirmed = userToEdit.EmailConfirmed;
            }
        }

        private async Task<IdentityResult> AddRolesToUserAsync(ApplicationUser user, IEnumerable<string> roles)
        {
            IdentityResult result;
            roles = RemoveDuplicateRoles(user, roles);
            result = await _userManager.AddToRolesAsync(user, roles);
            return result;
        }

        private List<string> RemoveDuplicateRoles(ApplicationUser user, IEnumerable<string> roles)
        {
            var userRoles = _userManager.GetRolesAsync(user).Result.ToList();
            var rolesToAdd = roles.Where(r => !userRoles.Contains(r)).ToList();
            return rolesToAdd;
        }

        private async Task<IdentityResult> RemoveRolesFromUserAsync(ApplicationUser user, IEnumerable<string> roles)
        {
            var currentUserRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentUserRoles);
            return await AddRolesToUserAsync(user, roles);

        }

        public async Task ChangeUserPassword(NewUserVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(NewUserVm).Name} cannot be null");
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            await _userManager.RemovePasswordAsync(user);
            var result = await _userManager.AddPasswordAsync(user, model.PasswordToChange);
        }
    }
}
