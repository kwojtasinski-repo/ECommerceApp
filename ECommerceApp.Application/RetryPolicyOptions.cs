using System;

namespace ECommerceApp.Application.Constants;

public sealed class RetryPolicyOptions
{
    public const string SectionName = "RetryPolicy";

    public int DefaultMaxRetries { get; init; } = 5;

    public int MaxRetriesCap { get; init; } = 10;

    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromHours(1);
}
