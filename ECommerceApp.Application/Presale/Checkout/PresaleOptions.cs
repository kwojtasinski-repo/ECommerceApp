using Microsoft.Extensions.Options;
using System;

namespace ECommerceApp.Application.Presale.Checkout
{
    public sealed class PresaleOptions
    {
        public const string SectionName = "Presale";

        /// <summary>User-visible reservation lifetime. Timer counts down to zero at this point.</summary>
        public TimeSpan SoftReservationTtl { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>Extra time added on top of <see cref="SoftReservationTtl"/> before the cleanup job fires.
        /// Ensures the backend never cleans up before the user-visible timer reaches zero.</summary>
        public TimeSpan SoftReservationGracePeriod { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>How long after the user-visible TTL a PlaceOrder request is still accepted.
        /// Covers slow connections / submit-on-last-second scenarios.</summary>
        public TimeSpan PlaceOrderAcceptanceWindow { get; set; } = TimeSpan.FromSeconds(15);
    }

    internal sealed class PresaleOptionsValidator : IValidateOptions<PresaleOptions>
    {
        public ValidateOptionsResult Validate(string name, PresaleOptions options)
        {
            if (options.SoftReservationTtl <= TimeSpan.Zero)
                return ValidateOptionsResult.Fail("Presale:SoftReservationTtl must be a positive duration.");

            if (options.SoftReservationGracePeriod < TimeSpan.Zero)
                return ValidateOptionsResult.Fail("Presale:SoftReservationGracePeriod must be zero or positive.");

            if (options.PlaceOrderAcceptanceWindow < TimeSpan.Zero)
                return ValidateOptionsResult.Fail("Presale:PlaceOrderAcceptanceWindow must be zero or positive.");

            if (options.PlaceOrderAcceptanceWindow >= options.SoftReservationGracePeriod)
                return ValidateOptionsResult.Fail("Presale:PlaceOrderAcceptanceWindow must be shorter than SoftReservationGracePeriod.");

            return ValidateOptionsResult.Success;
        }
    }
}
