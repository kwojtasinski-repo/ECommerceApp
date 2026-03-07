using Microsoft.Extensions.Options;
using System;

namespace ECommerceApp.Application.Presale.Checkout
{
    public sealed class PresaleOptions
    {
        public const string SectionName = "Presale";

        public TimeSpan SoftReservationTtl { get; set; } = TimeSpan.FromMinutes(15);
    }

    internal sealed class PresaleOptionsValidator : IValidateOptions<PresaleOptions>
    {
        public ValidateOptionsResult Validate(string? name, PresaleOptions options)
        {
            if (options.SoftReservationTtl <= TimeSpan.Zero)
                return ValidateOptionsResult.Fail("Presale:SoftReservationTtl must be a positive duration.");

            return ValidateOptionsResult.Success;
        }
    }
}
