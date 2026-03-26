using ECommerceApp.Domain.Identity.IAM;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Identity.IAM.Repositories
{
    internal sealed class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly IamDbContext _context;

        public RefreshTokenRepository(IamDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default)
        {
            await _context.RefreshTokens.AddAsync(refreshToken, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<RefreshToken> GetByTokenAsync(string token, CancellationToken ct = default)
        {
            return await _context.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == token, ct);
        }

        public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct = default)
        {
            _context.RefreshTokens.Update(refreshToken);
            await _context.SaveChangesAsync(ct);
        }

        public async Task RevokeAllForUserAsync(string userId, CancellationToken ct = default)
        {
            var tokens = await _context.RefreshTokens
                .Where(r => r.UserId == userId && !r.IsRevoked)
                .ToListAsync(ct);

            foreach (var token in tokens)
            {
                token.Revoke();
            }

            await _context.SaveChangesAsync(ct);
        }
    }
}
