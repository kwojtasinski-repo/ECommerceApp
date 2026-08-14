using System.Collections.Generic;

namespace ECommerceApp.Application.AccountProfile.Results
{
    public sealed record GuestAccountProvisioningResult(string UserId, IReadOnlyList<string> Errors)
    {
        public bool Succeeded => !string.IsNullOrWhiteSpace(UserId);
    }
}