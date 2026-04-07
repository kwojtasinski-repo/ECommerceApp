using ECommerceApp.Application.Supporting.Communication.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders.Adapters
{
    internal sealed class OrderUserResolverAdapter : IOrderUserResolver
    {
        private readonly OrdersDbContext _context;

        public OrderUserResolverAdapter(OrdersDbContext context)
        {
            _context = context;
        }

        public Task<string?> GetUserIdForOrderAsync(int orderId, CancellationToken ct = default)
            => _context.Orders
                .Where(o => o.Id.Value == orderId)
                .Select(o => (string?)o.UserId.Value)
                .FirstOrDefaultAsync(ct);
    }
}
