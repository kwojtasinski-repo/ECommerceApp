using ECommerceApp.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Payments.Repositories
{
    internal sealed class PaymentRepository : IPaymentRepository
    {
        private readonly PaymentsDbContext _context;

        public PaymentRepository(PaymentsDbContext context)
        {
            _context = context;
        }

        public async Task<Payment?> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == new PaymentId(id), ct);

        public async Task<Payment?> GetByOrderIdAsync(int orderId, CancellationToken ct = default)
            => await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.OrderId == new PaymentOrderId(orderId), ct);

        public async Task<Payment?> GetByPaymentIdAsync(Guid paymentId, string userId, CancellationToken ct = default)
            => await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId && p.UserId == userId, ct);

        public async Task<Payment?> GetPendingByOrderIdAsync(int orderId, string userId, CancellationToken ct = default)
            => await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.OrderId == new PaymentOrderId(orderId)
                                       && p.UserId == userId
                                       && p.Status == PaymentStatus.Pending, ct);

        public async Task<IReadOnlyList<Payment>> GetByUserIdAsync(string userId, CancellationToken ct = default)
            => await _context.Payments
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.ExpiresAt)
                .ToListAsync(ct);

        public async Task AddAsync(Payment payment, CancellationToken ct = default)
        {
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
        {
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync(ct);
        }
    }
}
