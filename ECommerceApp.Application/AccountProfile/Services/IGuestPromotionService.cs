using ECommerceApp.Application.AccountProfile.Results;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.AccountProfile.Services
{
    public interface IGuestPromotionService
    {
        Task<PromotionResult> PromoteAsync(int profileId, string requestingUserId, string password, CancellationToken ct = default);
    }
}