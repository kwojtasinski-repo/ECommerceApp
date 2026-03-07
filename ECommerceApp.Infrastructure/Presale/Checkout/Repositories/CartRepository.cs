using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Repositories
{
    internal sealed class CartRepository : ICartRepository
    {
        private readonly PresaleDbContext _context;

        public CartRepository(PresaleDbContext context)
        {
            _context = context;
        }

        public async Task<Cart?> GetByUserIdAsync(string userId, CancellationToken ct = default)
            => await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        public async Task<Cart?> GetByIdAsync(CartId id, CancellationToken ct = default)
            => await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task AddAsync(Cart cart, CancellationToken ct = default)
        {
            await _context.Carts.AddAsync(cart, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(Cart cart, CancellationToken ct = default)
        {
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Cart cart, CancellationToken ct = default)
        {
            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync(ct);
        }
    }
}
