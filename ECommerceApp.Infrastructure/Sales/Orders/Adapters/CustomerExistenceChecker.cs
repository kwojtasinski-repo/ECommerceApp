using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Domain.AccountProfile;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders.Adapters
{
    internal sealed class CustomerExistenceChecker : ICustomerExistenceChecker
    {
        private readonly IUserProfileRepository _userProfiles;

        public CustomerExistenceChecker(IUserProfileRepository userProfiles)
        {
            _userProfiles = userProfiles;
        }

        public Task<bool> ExistsAsync(int customerId, CancellationToken ct = default)
            => _userProfiles.ExistsByIdAsync(new UserProfileId(customerId));
    }
}
