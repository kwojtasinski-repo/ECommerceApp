using System;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public sealed class OrderAccessToken
    {
        public int Id { get; private set; }
        public int OrderId { get; private set; }
        public int UserProfileId { get; private set; }
        public string Token { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }

        private OrderAccessToken() { }

        public static OrderAccessToken Create(
            int orderId,
            int userProfileId,
            string token,
            DateTime? createdAt = null)
        {
            if (orderId <= 0)
                throw new ArgumentOutOfRangeException(nameof(orderId));

            if (userProfileId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userProfileId));

            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token is required.", nameof(token));

            return new OrderAccessToken
            {
                OrderId = orderId,
                UserProfileId = userProfileId,
                Token = token,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };
        }
    }
}
