using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    public interface IOrderAccessService
    {
        Task<OrderAccessScope> GetScopeAsync(string token, CancellationToken ct = default);
        Task<bool> HasAccessAsync(int orderId, string token, CancellationToken ct = default);
        Task<string> CreateAsync(int orderId, int userProfileId, string token, CancellationToken ct = default);
        Task<string> GetTokenForOrderAsync(int orderId, CancellationToken ct = default);
    }

    public sealed record OrderAccessScope(int OrderId, int UserProfileId);
}
