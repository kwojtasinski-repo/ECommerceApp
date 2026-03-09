using ECommerceApp.Domain.Sales.Orders;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders.Repositories
{
    internal sealed class OrderItemRepository : IOrderItemRepository
    {
        private readonly OrdersDbContext _context;

        public OrderItemRepository(OrdersDbContext context)
        {
            _context = context;
        }

        public async Task<OrderItem?> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.OrderItems
                .AsNoTracking()
                .FirstOrDefaultAsync(oi => oi.Id == new OrderItemId(id), ct);

        public async Task<int> AddAsync(OrderItem item, CancellationToken ct = default)
        {
            _context.OrderItems.Add(item);
            await _context.SaveChangesAsync(ct);
            return item.Id.Value;
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var item = await _context.OrderItems.FirstOrDefaultAsync(oi => oi.Id == new OrderItemId(id), ct);
            if (item is not null)
            {
                _context.OrderItems.Remove(item);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task<IReadOnlyList<OrderItem>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
        {
            var typedIds = ids.Select(id => new OrderItemId(id)).ToList();
            return await _context.OrderItems
                .AsNoTracking()
                .Where(oi => typedIds.Contains(oi.Id))
                .ToListAsync(ct);
        }

        public async Task AssignToOrderAsync(IReadOnlyList<int> itemIds, int orderId, CancellationToken ct = default)
        {
            var typedIds = itemIds.Select(id => new OrderItemId(id)).ToList();
            await _context.OrderItems
                .Where(oi => typedIds.Contains(oi.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(oi => oi.OrderId, (int?)orderId), ct);
        }

        public async Task<IReadOnlyList<OrderItem>> GetCartItemsByUserIdAsync(string userId, CancellationToken ct = default)
            => await _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.UserId == userId && oi.OrderId == null)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<int>> GetCartItemIdsByUserIdAsync(string userId, CancellationToken ct = default)
            => await _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.UserId == userId && oi.OrderId == null)
                .Select(oi => oi.Id.Value)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<OrderItem>> GetAllPagedAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default)
        {
            var query = _context.OrderItems.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(oi => oi.UserId.Contains(search));
            return await query
                .OrderBy(oi => oi.Id)
                .Skip(pageSize * (pageNo - 1))
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<int> GetAllPagedCountAsync(string? search, CancellationToken ct = default)
        {
            var query = _context.OrderItems.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(oi => oi.UserId.Contains(search));
            return await query.CountAsync(ct);
        }

        public async Task<int> GetCartItemCountByUserIdAsync(string userId, CancellationToken ct = default)
            => await _context.OrderItems
                .AsNoTracking()
                .CountAsync(oi => oi.UserId == userId && oi.OrderId == null, ct);
    }
}
