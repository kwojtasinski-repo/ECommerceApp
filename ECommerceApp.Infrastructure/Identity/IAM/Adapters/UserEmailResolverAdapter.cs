using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Domain.Identity.IAM;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Identity.IAM.Adapters
{
    internal sealed class UserEmailResolverAdapter : IUserEmailResolver
    {
        private readonly IUserManager<ApplicationUser> _userManager;

        public UserEmailResolverAdapter(IUserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<string?> GetEmailForUserAsync(string userId, CancellationToken ct = default)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user?.Email;
        }
    }
}
