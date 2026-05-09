using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders.Adapters
{
    internal sealed class OrderCustomerResolver : IOrderCustomerResolver
    {
        private const int PhoneContactTypeId = 1;
        private const int EmailContactTypeId = 2;

        private readonly Context _context;

        public OrderCustomerResolver(Context context)
        {
            _context = context;
        }

        public async Task<OrderCustomer> ResolveAsync(int customerId, CancellationToken ct = default)
        {
            var customer = await _context.Customers
                .Include(c => c.Addresses)
                .Include(c => c.ContactDetails)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId, ct);

            if (customer is null)
                throw new BusinessException($"Customer with id {customerId} was not found.");

            var address = customer.Addresses?.FirstOrDefault();
            var phone = customer.ContactDetails
                .FirstOrDefault(cd => cd.ContactDetailTypeId == PhoneContactTypeId)
                ?.ContactDetailInformation ?? string.Empty;
            var email = customer.ContactDetails
                .FirstOrDefault(cd => cd.ContactDetailTypeId == EmailContactTypeId)
                ?.ContactDetailInformation ?? string.Empty;

            return new OrderCustomer(
                firstName: customer.FirstName ?? string.Empty,
                lastName: customer.LastName ?? string.Empty,
                email: email,
                phoneNumber: phone,
                isCompany: customer.IsCompany,
                companyName: customer.CompanyName,
                nip: customer.NIP,
                street: address?.Street ?? string.Empty,
                buildingNumber: address?.BuildingNumber ?? string.Empty,
                flatNumber: address?.FlatNumber?.ToString(),
                zipCode: address?.ZipCode.ToString() ?? string.Empty,
                city: address?.City ?? string.Empty,
                country: address?.Country ?? string.Empty);
        }
    }
}
