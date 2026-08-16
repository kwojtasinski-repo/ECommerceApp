using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public interface IOrderAccessTokenRepository
    {
        Task AddAsync(OrderAccessToken accessToken, CancellationToken ct = default);
        Task<OrderAccessToken> GetByTokenAsync(string token, CancellationToken ct = default);
        Task<OrderAccessToken> GetByOrderIdAsync(int orderId, CancellationToken ct = default);
    }
}
