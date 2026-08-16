using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Supporting.Verification
{
    public sealed class VerificationCode
    {
        public int Id { get; private set; }
        public VerificationPurpose Purpose { get; private set; }
        public string SubjectKey { get; private set; } = default!;
        public string Code { get; private set; } = default!;
        public DateTime ExpiresAt { get; private set; }
        public DateTime? ConsumedAt { get; private set; }

        private VerificationCode() { }

        public static VerificationCode Create(
            VerificationPurpose purpose,
            string subjectKey,
            string code,
            DateTime expiresAt)
        {
            return new VerificationCode
            {
                Purpose = purpose,
                SubjectKey = subjectKey,
                Code = code,
                ExpiresAt = expiresAt
            };
        }

        public bool IsValid(DateTime now)
        {
            return !ConsumedAt.HasValue && now < ExpiresAt;
        }

        public void Consume()
        {
            if (ConsumedAt.HasValue)
            {
                throw new DomainException("Verification code has already been consumed.");
            }

            if (!IsValid(DateTime.UtcNow))
            {
                throw new DomainException("Verification code has expired.");
            }

            ConsumedAt = DateTime.UtcNow;
        }
    }
}