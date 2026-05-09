using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Identity.IAM.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeUserService : IBackofficeUserService
    {
        private readonly IUserManagementService _userManagement;

        public BackofficeUserService(IUserManagementService userManagement)
        {
            _userManagement = userManagement;
        }

        public async Task<BackofficeUserListVm> GetUsersAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var source = await _userManagement.GetUsersAsync(pageSize, pageNo, searchString ?? string.Empty);
            var roles = await Task.WhenAll(source.Users.Select(u => _userManagement.GetUserRoleAsync(u.Id)));

            var items = source.Users
                .Zip(roles, (u, role) => new BackofficeUserItemVm
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    Roles = string.IsNullOrEmpty(role)
                        ? new List<string>()
                        : new List<string> { role }
                })
                .ToList();

            return new BackofficeUserListVm
            {
                Users = items,
                CurrentPage = pageNo,
                PageSize = pageSize,
                TotalCount = source.Count,
                SearchString = searchString
            };
        }

        public async Task<BackofficeUserDetailVm> GetUserDetailAsync(string userId, CancellationToken ct = default)
        {
            try
            {
                var user = await _userManagement.GetUserByIdAsync(userId);
                var role = await _userManagement.GetUserRoleAsync(userId);
                return new BackofficeUserDetailVm
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Roles = string.IsNullOrEmpty(role)
                        ? new List<string>()
                        : new List<string> { role }
                };
            }
            catch (BusinessException)
            {
                return null;
            }
        }
    }
}

