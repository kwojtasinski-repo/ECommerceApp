using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Contracts
{
    public interface IVerificationCodeClient
    {
        Task<string> RequestOrderAccessRecoveryAsync(int orderId, CancellationToken ct = default);
        Task<OrderAccessRedemptionResult> RedeemOrderAccessRecoveryAsync(string code, CancellationToken ct = default);
    }

    public sealed record OrderAccessRedemptionResult(bool Success, int OrderId, int UserProfileId)
    {
        public static OrderAccessRedemptionResult Failed() => new(false, 0, 0);
        public static OrderAccessRedemptionResult Succeeded(int orderId, int userProfileId)
            => new(true, orderId, userProfileId);
    }
}
