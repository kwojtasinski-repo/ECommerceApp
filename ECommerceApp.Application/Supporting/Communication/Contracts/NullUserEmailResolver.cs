using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Contracts
{
    /// <summary>
    /// Null-object stub registered by default until an Infrastructure adapter is wired.
    /// Returns null — email handlers skip sending when the address cannot be resolved.
    /// </summary>
    internal sealed class NullUserEmailResolver : IUserEmailResolver
    {
        private readonly ILogger<NullUserEmailResolver> _logger;

        public NullUserEmailResolver(ILogger<NullUserEmailResolver> logger)
        {
            _logger = logger;
        }

        public Task<string?> GetEmailForUserAsync(string userId, CancellationToken ct = default)
        {
            _logger.LogWarning(
                "[Communication] No IUserEmailResolver configured — cannot resolve email for UserId={UserId}",
                userId);
            return Task.FromResult<string?>(null);
        }
    }
}
