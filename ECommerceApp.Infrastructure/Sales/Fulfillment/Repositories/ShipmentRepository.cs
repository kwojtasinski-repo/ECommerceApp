using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Domain.Sales.Fulfillment;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Fulfillment.Repositories
{
    internal sealed class ShipmentRepository : IShipmentRepository
    {
        private readonly FulfillmentDbContext _context;

        public ShipmentRepository(FulfillmentDbContext context)
        {
            _context = context;
        }

        public async Task<Shipment?> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.Shipments
                .Include(s => s.Lines)
                .FirstOrDefaultAsync(s => s.Id == new ShipmentId(id), ct);

        public async Task<int> AddAsync(Shipment shipment, CancellationToken ct = default)
        {
            _context.Shipments.Add(shipment);
            await _context.SaveChangesAsync(ct);
            return shipment.Id.Value;
        }

        public async Task UpdateAsync(Shipment shipment, CancellationToken ct = default)
        {
            _context.Shipments.Update(shipment);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<Shipment>> GetByOrderIdAsync(int orderId, CancellationToken ct = default)
            => await _context.Shipments
                .AsNoTracking()
                .Include(s => s.Lines)
                .Where(s => s.OrderId == orderId)
                .ToListAsync(ct);
    }
}
