using System.Collections.Generic;

namespace ECommerceApp.Application.AccountProfile.Results
{
    public enum PromotionStatus
    {
        Success,
        ProfileNotFound,
        NotOwner,
        AlreadyRegistered,
        IdentityCreationFailed
    }

    public sealed record PromotionResult(PromotionStatus Status, IReadOnlyList<string> Errors)
    {
        public static PromotionResult Success() => new(PromotionStatus.Success, new List<string>());
        public static PromotionResult ProfileNotFound() => new(PromotionStatus.ProfileNotFound, new List<string>());
        public static PromotionResult NotOwner() => new(PromotionStatus.NotOwner, new List<string>());
        public static PromotionResult AlreadyRegistered() => new(PromotionStatus.AlreadyRegistered, new List<string>());
        public static PromotionResult IdentityCreationFailed(IEnumerable<string> errors)
            => new(PromotionStatus.IdentityCreationFailed, new List<string>(errors));
    }
}