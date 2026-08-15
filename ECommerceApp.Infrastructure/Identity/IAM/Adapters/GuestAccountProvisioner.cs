using ECommerceApp.Application.AccountProfile.Results;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Identity.IAM;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Identity.IAM.Adapters
{
    internal sealed class GuestAccountProvisioner : IGuestAccountProvisioner
    {
        private readonly IUserManager<ApplicationUser> _userManager;

        public GuestAccountProvisioner(IUserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<bool> IsRegisteredAsync(string userId)
            => await _userManager.FindByIdAsync(userId) is not null;

        public async Task<IReadOnlySet<string>> GetRegisteredUserIdsAsync(IReadOnlyCollection<string> userIds)
        {
            var found = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();
            return found.ToHashSet();
        }

        public async Task<GuestAccountProvisioningResult> CreateAsync(string email, string password)
        {
            var user = new ApplicationUser { UserName = email, Email = email };
            var result = await _userManager.CreateAsync(user, password);
            return result.Succeeded
                ? new GuestAccountProvisioningResult(user.Id, new List<string>())
                : new GuestAccountProvisioningResult(null, result.Errors.Select(error => error.Description).ToList());
        }

        public async Task DeleteAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is not null)
                await _userManager.DeleteAsync(user);
        }
    }
}