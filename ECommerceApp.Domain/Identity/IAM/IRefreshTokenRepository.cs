using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Identity.IAM
{
    public interface IRefreshTokenRepository
    {
        Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default);
        Task<RefreshToken> GetByTokenAsync(string token, CancellationToken ct = default);
        Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct = default);
        Task RevokeAllForUserAsync(string userId, CancellationToken ct = default);
    }
}
