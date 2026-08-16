using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.AccountProfile.Contracts
{
    public interface IVerificationCodeClient
    {
        Task<string> RequestGuestAccountLinkAsync(string email, CancellationToken ct = default);
        Task<GuestLinkRedemptionResult> RedeemGuestAccountLinkAsync(string code, string newUserId, CancellationToken ct = default);
    }

    public sealed record GuestLinkRedemptionResult(bool Success, int ProfilesLinked);
}
