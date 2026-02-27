using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Interfaces
{
    public interface IUserManager<TUser> : IDisposable where TUser : class
    {
        IQueryable<TUser> Users { get; }

        Task<TUser> FindByNameAsync(string userName);
        Task<TUser> FindByIdAsync(string userId);
        Task<IdentityResult> CreateAsync(TUser user, string password);
        Task<IdentityResult> UpdateAsync(TUser user);
        Task<IdentityResult> DeleteAsync(TUser user);
        Task<IdentityResult> AddPasswordAsync(TUser user, string password);
        Task<IdentityResult> RemovePasswordAsync(TUser user);
        Task<IList<string>> GetRolesAsync(TUser user);
        Task<IdentityResult> AddToRolesAsync(TUser user, IEnumerable<string> roles);
        Task<IdentityResult> RemoveFromRoleAsync(TUser user, string role);
        Task<IdentityResult> RemoveFromRolesAsync(TUser user, IEnumerable<string> roles);
    }
}
