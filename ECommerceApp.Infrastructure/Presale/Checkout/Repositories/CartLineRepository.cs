using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Repositories
{
    internal sealed class CartLineRepository : ICartLineRepository
    {
        private readonly PresaleDbContext _context;

        public CartLineRepository(PresaleDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<CartLine>> GetByUserIdAsync(PresaleUserId userId, CancellationToken ct = default)
            => await _context.CartLines
                .Where(c => c.UserId == userId)
                .ToListAsync(ct);

        public async Task UpsertAsync(CartLine cartLine, CancellationToken ct = default)
        {
            var existing = await _context.CartLines
                .FirstOrDefaultAsync(c => c.UserId == cartLine.UserId
                                       && c.ProductId == cartLine.ProductId, ct);
            if (existing is null)
            {
                _context.CartLines.Add(cartLine);
            }
            else
            {
                existing.UpdateQuantity(cartLine.Quantity.Value);
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(PresaleUserId userId, PresaleProductId productId, CancellationToken ct = default)
        {
            var line = await _context.CartLines
                .FirstOrDefaultAsync(c => c.UserId == userId
                                       && c.ProductId == productId, ct);
            if (line is not null)
            {
                _context.CartLines.Remove(line);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task DeleteAllForUserAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            var lines = await _context.CartLines
                .Where(c => c.UserId == userId)
                .ToListAsync(ct);
            _context.CartLines.RemoveRange(lines);
            await _context.SaveChangesAsync(ct);
        }
    }
}
