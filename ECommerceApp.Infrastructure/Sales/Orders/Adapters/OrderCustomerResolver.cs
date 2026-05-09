using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.Sales.Orders;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders.Adapters
{
    internal sealed class OrderCustomerResolver : IOrderCustomerResolver
    {
        private readonly IUserProfileRepository _userProfiles;

        public OrderCustomerResolver(IUserProfileRepository userProfiles)
        {
            _userProfiles = userProfiles;
        }

        public async Task<OrderCustomer> ResolveAsync(int customerId, CancellationToken ct = default)
        {
            var profile = await _userProfiles.GetByIdAsync(new UserProfileId(customerId));

            if (profile is null)
                throw new BusinessException($"Customer with id {customerId} was not found.");

            var address = profile.Addresses.FirstOrDefault();

            return new OrderCustomer(
                firstName: profile.FirstName,
                lastName: profile.LastName,
                email: profile.Email.Value,
                phoneNumber: profile.PhoneNumber.Value,
                isCompany: profile.IsCompany,
                companyName: profile.CompanyName?.Value ?? string.Empty,
                nip: profile.NIP?.Value ?? string.Empty,
                street: address?.Street.Value ?? string.Empty,
                buildingNumber: address?.BuildingNumber.Value ?? string.Empty,
                flatNumber: address?.FlatNumber.Value.ToString(),
                zipCode: address?.ZipCode.Value ?? string.Empty,
                city: address?.City.Value ?? string.Empty,
                country: address?.Country.Value ?? string.Empty);
        }
    }
}
