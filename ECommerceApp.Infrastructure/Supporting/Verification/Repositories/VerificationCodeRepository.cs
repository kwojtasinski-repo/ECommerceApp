using ECommerceApp.Domain.Supporting.Verification;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.Verification.Repositories
{
    internal sealed class VerificationCodeRepository : IVerificationCodeRepository
    {
        private readonly IVerificationDbContext _context;

        public VerificationCodeRepository(IVerificationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(VerificationCode verificationCode, CancellationToken ct = default)
        {
            _context.VerificationCodes.Add(verificationCode);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<VerificationCode> GetByCodeAsync(
            string code,
            VerificationPurpose purpose,
            CancellationToken ct = default)
        {
            return await _context.VerificationCodes
                .FirstOrDefaultAsync(x => x.Code == code && x.Purpose == purpose, ct);
        }

        public async Task<IReadOnlyList<VerificationCode>> GetPendingAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return await _context.VerificationCodes
                .AsNoTracking()
                .Where(x => !x.ConsumedAt.HasValue && x.ExpiresAt > now)
                .ToListAsync(ct);
        }

        public async Task UpdateAsync(VerificationCode verificationCode, CancellationToken ct = default)
        {
            _context.VerificationCodes.Update(verificationCode);
            await _context.SaveChangesAsync(ct);
        }
    }
}