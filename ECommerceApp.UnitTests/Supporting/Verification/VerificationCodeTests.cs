using ECommerceApp.Domain.Supporting.Verification;
using AwesomeAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.Verification
{
    public class VerificationCodeTests
    {
        private static VerificationCode CreateCode(DateTime expiresAt, DateTime? consumedAt = null)
        {
            var code = VerificationCode.Create(
                VerificationPurpose.GuestAccountLink,
                "subject",
                "code",
                expiresAt);

            if (consumedAt.HasValue)
            {
                code.Consume();
            }

            return code;
        }

        [Fact]
        public void IsValid_NotYetExpiredAndUnconsumed_ReturnsTrue()
        {
            var now = DateTime.UtcNow;
            var code = CreateCode(now.AddMinutes(5));

            code.IsValid(now).Should().BeTrue();
        }

        [Fact]
        public void IsValid_Expired_ReturnsFalse()
        {
            var now = DateTime.UtcNow;
            var code = CreateCode(now.AddMinutes(-1));

            code.IsValid(now).Should().BeFalse();
        }

        [Fact]
        public void IsValid_AlreadyConsumed_ReturnsFalse()
        {
            var code = CreateCode(DateTime.UtcNow.AddMinutes(5));
            code.Consume();

            code.IsValid(DateTime.UtcNow).Should().BeFalse();
        }

        [Fact]
        public void Consume_Valid_SetsConsumedAt()
        {
            var code = CreateCode(DateTime.UtcNow.AddMinutes(5));

            code.Consume();

            code.ConsumedAt.Should().NotBeNull();
        }

        [Fact]
        public void Consume_AlreadyConsumed_Throws()
        {
            var code = CreateCode(DateTime.UtcNow.AddMinutes(5));
            code.Consume();

            Action consume = code.Consume;

            consume.Should().Throw<Domain.Shared.DomainException>();
        }

        [Fact]
        public void Consume_Expired_Throws()
        {
            var code = CreateCode(DateTime.UtcNow.AddMinutes(-1));

            Action consume = code.Consume;

            consume.Should().Throw<Domain.Shared.DomainException>();
        }
    }
}