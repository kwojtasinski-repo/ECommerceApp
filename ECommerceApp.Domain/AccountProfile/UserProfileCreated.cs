using System;

namespace ECommerceApp.Domain.AccountProfile
{
    public record UserProfileCreated(int UserProfileId, string UserId, DateTime OccurredAt);
}
