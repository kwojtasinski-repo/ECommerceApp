using System;

namespace ECommerceApp.Domain.Identity.IAM
{
    public class RefreshToken
    {
        public int Id { get; private set; }
        public string UserId { get; private set; }
        public string Token { get; private set; }
        public string JwtId { get; private set; }
        public bool IsRevoked { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private RefreshToken() { }

        public static RefreshToken Create(string userId, string token, string jwtId, DateTime expiresAt)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("UserId is required.", nameof(userId));
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Token is required.", nameof(token));
            if (string.IsNullOrWhiteSpace(jwtId)) throw new ArgumentException("JwtId is required.", nameof(jwtId));

            return new RefreshToken
            {
                UserId = userId,
                Token = token,
                JwtId = jwtId,
                IsRevoked = false,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Revoke()
        {
            if (IsRevoked)
            {
                throw new InvalidOperationException("Token is already revoked.");
            }

            IsRevoked = true;
        }
    }
}
