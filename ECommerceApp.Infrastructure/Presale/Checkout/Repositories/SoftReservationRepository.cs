using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Repositories
{
    internal sealed class SoftReservationRepository : ISoftReservationRepository
    {
        private readonly PresaleDbContext _context;

        public SoftReservationRepository(PresaleDbContext context)
        {
            _context = context;
        }

        public async Task<SoftReservation?> GetByIdAsync(SoftReservationId id, CancellationToken ct = default)
            => await _context.SoftReservations
                .FirstOrDefaultAsync(r => r.Id == id, ct);

        public async Task<SoftReservation?> FindAsync(PresaleProductId productId, PresaleUserId userId, CancellationToken ct = default)
            => await _context.SoftReservations
                .FirstOrDefaultAsync(r => r.ProductId == productId
                                       && r.UserId == userId, ct);

        public async Task<IReadOnlyList<SoftReservation>> GetByProductIdAsync(PresaleProductId productId, CancellationToken ct = default)
            => await _context.SoftReservations
                .Where(r => r.ProductId == productId)
                .ToListAsync(ct);

        public async Task AddAsync(SoftReservation reservation, CancellationToken ct = default)
        {
            _context.SoftReservations.Add(reservation);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(SoftReservation reservation, CancellationToken ct = default)
        {
            _context.SoftReservations.Remove(reservation);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAllForProductAsync(PresaleProductId productId, CancellationToken ct = default)
        {
            var reservations = await _context.SoftReservations
                .Where(r => r.ProductId == productId)
                .ToListAsync(ct);
            _context.SoftReservations.RemoveRange(reservations);
            await _context.SaveChangesAsync(ct);
        }
    }
}
