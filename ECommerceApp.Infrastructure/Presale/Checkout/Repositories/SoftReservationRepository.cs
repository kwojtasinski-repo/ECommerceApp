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
        private readonly IPresaleDbContext _context;

        public SoftReservationRepository(IPresaleDbContext context)
        {
            _context = context;
        }

        public async Task<SoftReservation> GetByIdAsync(SoftReservationId id, CancellationToken ct = default)
            => await _context.SoftReservations
                .FirstOrDefaultAsync(r => r.Id == id, ct);

        public async Task<SoftReservation> FindAsync(PresaleProductId productId, PresaleUserId userId, CancellationToken ct = default)
            => await _context.SoftReservations
                .FirstOrDefaultAsync(r => r.ProductId == productId
                                       && r.UserId == userId, ct);

        public async Task<IReadOnlyList<SoftReservation>> GetByProductIdAsync(PresaleProductId productId, CancellationToken ct = default)
            => await _context.SoftReservations
                .Where(r => r.ProductId == productId)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<SoftReservation>> GetByUserIdAsync(PresaleUserId userId, CancellationToken ct = default)
            => await _context.SoftReservations
                .Where(r => r.UserId == userId)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<SoftReservation>> GetActiveByUserIdAsync(PresaleUserId userId, CancellationToken ct = default)
            => await _context.SoftReservations
                .Where(r => r.UserId == userId && r.Status == SoftReservationStatus.Active)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<SoftReservation>> GetCommittedByUserIdAsync(PresaleUserId userId, CancellationToken ct = default)
            => await _context.SoftReservations
                .Where(r => r.UserId == userId && r.Status == SoftReservationStatus.Committed)
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

        public async Task DeleteAllForUserAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            var reservations = await _context.SoftReservations
                .Where(r => r.UserId == userId)
                .ToListAsync(ct);
            _context.SoftReservations.RemoveRange(reservations);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteCommittedForUserAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            var reservations = await _context.SoftReservations
                .Where(r => r.UserId == userId && r.Status == SoftReservationStatus.Committed)
                .ToListAsync(ct);
            _context.SoftReservations.RemoveRange(reservations);
            await _context.SaveChangesAsync(ct);
        }

        public async Task CommitAllForUserAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            var reservations = await _context.SoftReservations
                .Where(r => r.UserId == userId && r.Status == SoftReservationStatus.Active)
                .ToListAsync(ct);
            foreach (var r in reservations)
                r.Commit();
            await _context.SaveChangesAsync(ct);
        }

        public async Task RevertAllForUserAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            var reservations = await _context.SoftReservations
                .Where(r => r.UserId == userId && r.Status == SoftReservationStatus.Committed)
                .ToListAsync(ct);
            foreach (var r in reservations)
                r.Revert();
            await _context.SaveChangesAsync(ct);
        }
    }
}

