using ECommerceApp.Domain.Sales.Orders;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders.Repositories
{
    internal sealed class OrderRepository : IOrderRepository
    {
        private readonly IOrdersDbContext _context;

        public OrderRepository(IOrdersDbContext context)
        {
            _context = context;
        }

        public async Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == new OrderId(id), ct);

        public async Task<Order?> GetByIdWithItemsAsync(int id, CancellationToken ct = default)
            => await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Events)
                .FirstOrDefaultAsync(o => o.Id == new OrderId(id), ct);

        public async Task<Order?> GetByRefundIdWithItemsAsync(int refundId, CancellationToken ct = default)
        {
            var payload = $"{{\"RefundId\":{refundId}}}";
            var orderId = await _context.OrderEvents
                .Where(e => e.EventType == OrderEventType.RefundAssigned && e.Payload == payload)
                .Select(e => e.OrderId)
                .FirstOrDefaultAsync(ct);

            if (orderId is null)
                return null;

            return await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        }

        public async Task<int> AddAsync(Order order, CancellationToken ct = default)
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync(ct);
            return order.Id.Value;
        }

        public async Task UpdateAsync(Order order, CancellationToken ct = default)
        {
            _context.Orders.Update(order);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == new OrderId(id), ct);
            if (order is not null)
            {
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task<IReadOnlyList<Order>> GetAllAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default)
        {
            var query = _context.Orders.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(o => o.Number.Value.Contains(search));
            return await query
                .OrderByDescending(o => o.Ordered)
                .Skip(pageSize * (pageNo - 1))
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<int> GetAllCountAsync(string? search, CancellationToken ct = default)
        {
            var query = _context.Orders.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(o => o.Number.Value.Contains(search));
            return await query.CountAsync(ct);
        }

        public async Task<IReadOnlyList<Order>> GetByUserIdAsync(string userId, CancellationToken ct = default)
        {
            OrderUserId typedUserId = userId;
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == typedUserId)
                .OrderByDescending(o => o.Ordered)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Order>> GetByCustomerIdAsync(int customerId, CancellationToken ct = default)
            => await _context.Orders
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.Ordered)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<Order>> GetAllPaidAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default)
        {
            var query = _context.Orders.AsNoTracking().Where(o =>
                o.Status == OrderStatus.PaymentConfirmed ||
                o.Status == OrderStatus.PartiallyFulfilled ||
                o.Status == OrderStatus.Fulfilled);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(o => o.Number.Value.Contains(search));
            return await query
                .OrderByDescending(o => o.Ordered)
                .Skip(pageSize * (pageNo - 1))
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<int> GetAllPaidCountAsync(string? search, CancellationToken ct = default)
        {
            var query = _context.Orders.AsNoTracking().Where(o =>
                o.Status == OrderStatus.PaymentConfirmed ||
                o.Status == OrderStatus.PartiallyFulfilled ||
                o.Status == OrderStatus.Fulfilled);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(o => o.Number.Value.Contains(search));
            return await query.CountAsync(ct);
        }

        public async Task<int?> GetCustomerIdAsync(int orderId, CancellationToken ct = default)
            => await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == new OrderId(orderId))
                .Select(o => (int?)o.CustomerId)
                .FirstOrDefaultAsync(ct);
    }
}
