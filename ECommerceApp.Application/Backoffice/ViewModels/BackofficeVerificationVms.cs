using ECommerceApp.Domain.Supporting.Verification;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Backoffice.ViewModels
{
    public sealed class BackofficeVerificationListVm
    {
        public VerificationPurpose? PurposeFilter { get; init; }
        public IReadOnlyList<BackofficeVerificationItemVm> Codes { get; init; } = new List<BackofficeVerificationItemVm>();
    }

    public sealed class BackofficeVerificationItemVm
    {
        public VerificationPurpose Purpose { get; init; }
        public string SubjectKey { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
        public string RedemptionUrl { get; set; } = string.Empty;
    }
}
