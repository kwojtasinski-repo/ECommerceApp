using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Repositories
{
    internal sealed class OrderAccessTokenRepository : IOrderAccessTokenRepository
    {
        private readonly IPresaleDbContext _context;

        public OrderAccessTokenRepository(IPresaleDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(OrderAccessToken accessToken, CancellationToken ct = default)
        {
            _context.OrderAccessTokens.Add(accessToken);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<OrderAccessToken> GetByTokenAsync(string token, CancellationToken ct = default)
            => await _context.OrderAccessTokens
                .AsNoTracking()
                .SingleOrDefaultAsync(accessToken => accessToken.Token == token, ct);

        public async Task<OrderAccessToken> GetByOrderIdAsync(int orderId, CancellationToken ct = default)
            => await _context.OrderAccessTokens
                .AsNoTracking()
                .Where(accessToken => accessToken.OrderId == orderId)
                .OrderByDescending(accessToken => accessToken.CreatedAt)
                .FirstOrDefaultAsync(ct);
    }
}
