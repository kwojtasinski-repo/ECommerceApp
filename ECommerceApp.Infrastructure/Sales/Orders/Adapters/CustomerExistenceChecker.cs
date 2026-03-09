using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders.Adapters
{
    internal sealed class CustomerExistenceChecker : ICustomerExistenceChecker
    {
        private readonly Context _context;

        public CustomerExistenceChecker(Context context)
        {
            _context = context;
        }

        public Task<bool> ExistsAsync(int customerId, CancellationToken ct = default)
            => _context.Customers.AnyAsync(c => c.Id == customerId, ct);
    }
}
