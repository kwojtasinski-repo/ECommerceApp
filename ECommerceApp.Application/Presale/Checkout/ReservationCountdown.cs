using System;

namespace ECommerceApp.Application.Presale.Checkout
{
    /// <summary>
    /// Value object representing the remaining seconds for a soft reservation.
    /// Always computed server-side from a request-start timestamp so the client
    /// never needs to parse dates or trust its own clock.
    /// </summary>
    public readonly struct ReservationCountdown
    {
        public int Seconds { get; }
        public bool IsExpired => Seconds == 0;

        private ReservationCountdown(int seconds) => Seconds = seconds;

        /// <summary>
        /// Computes remaining seconds between <paramref name="expiresAt"/> and
        /// <paramref name="requestStartedAt"/> (captured before DB calls so that
        /// query latency is already deducted from the result).
        /// Returns <see cref="Expired"/> when the deadline has already passed.
        /// </summary>
        public static ReservationCountdown From(DateTime expiresAt, DateTime requestStartedAt)
        {
            var diff = (int)(expiresAt - requestStartedAt).TotalSeconds;
            return new(diff > 0 ? diff : 0);
        }

        public static ReservationCountdown Expired => new(0);
    }
}
