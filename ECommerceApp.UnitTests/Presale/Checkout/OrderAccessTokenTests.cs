using ECommerceApp.Domain.Presale.Checkout;
using AwesomeAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class OrderAccessTokenTests
    {
        [Fact]
        public void Create_StoresOrderScopeAndTimestamp()
        {
            var createdAt = DateTime.UtcNow.AddMinutes(-1);

            var token = OrderAccessToken.Create(42, 7, "oat_abcdef", createdAt);

            token.OrderId.Should().Be(42);
            token.UserProfileId.Should().Be(7);
            token.Token.Should().Be("oat_abcdef");
            token.CreatedAt.Should().Be(createdAt);
        }

        [Fact]
        public void Create_RejectsInvalidScope()
        {
            var act = () => OrderAccessToken.Create(0, 7, "oat_token");

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Create_RejectsBlankToken()
        {
            var act = () => OrderAccessToken.Create(42, 7, " ");

            act.Should().Throw<ArgumentException>();
        }
    }
}
