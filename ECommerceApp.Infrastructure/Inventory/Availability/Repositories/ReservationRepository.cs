using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Inventory.Availability.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Inventory.Availability.Repositories
{
    internal sealed class ReservationRepository : IReservationRepository
    {
        private readonly AvailabilityDbContext _context;

        public ReservationRepository(AvailabilityDbContext context)
        {
            _context = context;
        }

        public async Task<Reservation?> GetByOrderAndProductAsync(int orderId, int productId, CancellationToken ct = default)
            => await _context.Reservations
                .FirstOrDefaultAsync(r => r.OrderId == new ReservationOrderId(orderId) && r.ProductId == new StockProductId(productId), ct);

        public async Task<IReadOnlyList<Reservation>> GetByOrderIdAsync(int orderId, CancellationToken ct = default)
            => await _context.Reservations
                .AsNoTracking()
                .Where(r => r.OrderId == new ReservationOrderId(orderId))
                .ToListAsync(ct);

        public async Task AddAsync(Reservation reservation, CancellationToken ct = default)
        {
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(Reservation reservation, CancellationToken ct = default)
        {
            _context.Reservations.Update(reservation);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Reservation reservation, CancellationToken ct = default)
        {
            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync(ct);
        }
    }
}
