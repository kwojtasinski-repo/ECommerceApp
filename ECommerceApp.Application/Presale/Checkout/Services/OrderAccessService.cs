using ECommerceApp.Domain.Presale.Checkout;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class OrderAccessService : IOrderAccessService
    {
        private readonly IOrderAccessTokenRepository _repository;

        public OrderAccessService(IOrderAccessTokenRepository repository)
        {
            _repository = repository;
        }

        public async Task<OrderAccessScope> GetScopeAsync(string token, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var accessToken = await _repository.GetByTokenAsync(token, ct);
            return accessToken is null
                ? null
                : new OrderAccessScope(accessToken.OrderId, accessToken.UserProfileId);
        }

        public async Task<bool> HasAccessAsync(int orderId, string token, CancellationToken ct = default)
        {
            var scope = await GetScopeAsync(token, ct);
            return scope is not null && scope.OrderId == orderId;
        }

        public async Task<string> CreateAsync(
            int orderId,
            int userProfileId,
            string token,
            CancellationToken ct = default)
        {
            await _repository.AddAsync(OrderAccessToken.Create(orderId, userProfileId, token), ct);
            return token;
        }

        public async Task<string> GetTokenForOrderAsync(int orderId, CancellationToken ct = default)
        {
            var accessToken = await _repository.GetByOrderIdAsync(orderId, ct);
            return accessToken?.Token;
        }
    }
}
