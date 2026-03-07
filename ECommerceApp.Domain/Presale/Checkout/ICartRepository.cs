using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public interface ICartRepository
    {
        Task<Cart?> GetByUserIdAsync(string userId, CancellationToken ct = default);
        Task<Cart?> GetByIdAsync(CartId id, CancellationToken ct = default);
        Task AddAsync(Cart cart, CancellationToken ct = default);
        Task UpdateAsync(Cart cart, CancellationToken ct = default);
        Task DeleteAsync(Cart cart, CancellationToken ct = default);
    }
}
