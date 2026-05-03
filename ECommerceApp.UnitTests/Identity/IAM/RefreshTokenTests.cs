using ECommerceApp.Domain.Identity.IAM;
using AwesomeAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Identity.IAM
{
    public class RefreshTokenTests
    {
        [Fact]
        public void Create_WithValidData_ShouldReturnRefreshToken()
        {
            var userId = "user-1";
            var token = "some-opaque-token";
            var jwtId = Guid.NewGuid().ToString();
            var expiresAt = DateTime.UtcNow.AddDays(7);

            var refreshToken = RefreshToken.Create(userId, token, jwtId, expiresAt);

            refreshToken.UserId.Should().Be(userId);
            refreshToken.Token.Should().Be(token);
            refreshToken.JwtId.Should().Be(jwtId);
            refreshToken.ExpiresAt.Should().Be(expiresAt);
            refreshToken.IsRevoked.Should().BeFalse();
            refreshToken.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithInvalidUserId_ShouldThrowArgumentException(string userId)
        {
            Action act = () => RefreshToken.Create(userId, "token", "jti", DateTime.UtcNow.AddDays(7));

            act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("userId");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithInvalidToken_ShouldThrowArgumentException(string token)
        {
            Action act = () => RefreshToken.Create("user-1", token, "jti", DateTime.UtcNow.AddDays(7));

            act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("token");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithInvalidJwtId_ShouldThrowArgumentException(string jwtId)
        {
            Action act = () => RefreshToken.Create("user-1", "token", jwtId, DateTime.UtcNow.AddDays(7));

            act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("jwtId");
        }

        [Fact]
        public void Revoke_ShouldSetIsRevokedToTrue()
        {
            var refreshToken = RefreshToken.Create("user-1", "token", "jti", DateTime.UtcNow.AddDays(7));

            refreshToken.Revoke();

            refreshToken.IsRevoked.Should().BeTrue();
        }

        [Fact]
        public void Revoke_WhenAlreadyRevoked_ShouldThrowInvalidOperationException()
        {
            var refreshToken = RefreshToken.Create("user-1", "token", "jti", DateTime.UtcNow.AddDays(7));
            refreshToken.Revoke();

            Action act = () => refreshToken.Revoke();

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("Token is already revoked.");
        }
    }
}
