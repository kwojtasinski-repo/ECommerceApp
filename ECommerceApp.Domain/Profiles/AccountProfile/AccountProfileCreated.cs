using System;

namespace ECommerceApp.Domain.Profiles.AccountProfile
{
    public record AccountProfileCreated(int AccountProfileId, string UserId, DateTime OccurredAt);
}
