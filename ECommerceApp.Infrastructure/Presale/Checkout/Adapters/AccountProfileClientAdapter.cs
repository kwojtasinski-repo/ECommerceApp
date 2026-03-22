using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Adapters
{
    internal sealed class AccountProfileClientAdapter : IAccountProfileClient
    {
        private readonly IUserProfileService _userProfileService;

        public AccountProfileClientAdapter(IUserProfileService userProfileService)
        {
            _userProfileService = userProfileService;
        }

        public async Task<CheckoutProfileVm?> GetProfileAsync(string userId, CancellationToken ct = default)
        {
            var profile = await _userProfileService.GetDetailsByUserIdAsync(userId);
            if (profile is null)
                return null;

            var addr = profile.Addresses.FirstOrDefault();
            return new CheckoutProfileVm(
                profile.Id,
                profile.FirstName,
                profile.LastName,
                profile.Email,
                profile.PhoneNumber,
                profile.IsCompany,
                profile.CompanyName,
                profile.NIP,
                addr?.Street,
                addr?.BuildingNumber,
                addr?.FlatNumber?.ToString(),
                addr?.ZipCode,
                addr?.City,
                addr?.Country);
        }
    }
}
