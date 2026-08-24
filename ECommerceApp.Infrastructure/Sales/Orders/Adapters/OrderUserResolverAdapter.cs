using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Domain.Sales.Orders;
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

        public Task<string> GetUserIdForOrderAsync(int orderId, CancellationToken ct = default)
            => _context.Orders
                .Where(o => o.Id == new OrderId(orderId))
                .Select(o => (string)o.UserId.Value)
                .FirstOrDefaultAsync(ct);
    }
}
