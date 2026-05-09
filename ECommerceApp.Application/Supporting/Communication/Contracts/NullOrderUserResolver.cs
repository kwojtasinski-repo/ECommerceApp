using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Contracts
{
    /// <summary>
    /// Null-object stub registered by default until an Infrastructure adapter is wired.
    /// Returns null for every order — notification handlers skip sending when userId is null.
    /// </summary>
    internal sealed class NullOrderUserResolver : IOrderUserResolver
    {
        private readonly ILogger<NullOrderUserResolver> _logger;

        public NullOrderUserResolver(ILogger<NullOrderUserResolver> logger)
        {
            _logger = logger;
        }

        public Task<string> GetUserIdForOrderAsync(int orderId, CancellationToken ct = default)
        {
            _logger.LogWarning(
                "[Communication] No IOrderUserResolver configured — skipping notification for OrderId={OrderId}",
                orderId);
            return Task.FromResult<string>(null);
        }
    }
}
