using ECommerceApp.Application.AccountProfile.Contracts;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Supporting.Verification.Services;
using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.Supporting.Verification;
using ECommerceApp.Infrastructure.AccountProfile.Adapters;
using AwesomeAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.AccountProfile
{
    public class VerificationCodeClientAdapterTests
    {
        private readonly Mock<IVerificationCodeService> _verificationCodes = new();
        private readonly Mock<IUserProfileRepository> _profiles = new();
        private readonly Mock<IGuestAccountProvisioner> _users = new();

        private void SetupAccountLinkCodeGeneration()
        {
            _verificationCodes.Setup(service => service.GenerateAsync(
                    VerificationPurpose.GuestAccountLink,
                    "guest@test.com",
                    It.IsAny<System.TimeSpan>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync("generated-code");
        }

        private void SetupAccountLinkRedemption(List<UserProfile> profiles)
        {
            _verificationCodes.Setup(service => service.TryConsumeAsync(
                    "code",
                    VerificationPurpose.GuestAccountLink,
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync("guest@test.com");
            _profiles.Setup(repository => repository.GetByEmailAsync("guest@test.com", true))
                .ReturnsAsync(profiles);
            _users.Setup(users => users.GetRegisteredUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>()))
                .ReturnsAsync(new HashSet<string>());
        }

        [Fact]
        public async Task RequestGuestAccountLinkAsync_DelegatesWithGuestAccountLinkPurpose()
        {
            // Arrange
            SetupAccountLinkCodeGeneration();

            // Act
            var result = await CreateAdapter().RequestGuestAccountLinkAsync(
                "guest@test.com",
                TestContext.Current.CancellationToken);

            // Assert
            result.Should().Be("generated-code");
            _verificationCodes.Verify(service => service.GenerateAsync(
                VerificationPurpose.GuestAccountLink,
                "guest@test.com",
                It.IsAny<System.TimeSpan>(),
                It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RedeemGuestAccountLinkAsync_ValidCode_ReassignsAllUnclaimedProfilesToCaller()
        {
            // Arrange
            var first = CreateProfile("gst_1");
            var second = CreateProfile("gst_2");
            SetupAccountLinkRedemption(new List<UserProfile> { first, second });

            // Act
            var result = await CreateAdapter().RedeemGuestAccountLinkAsync(
                "code",
                "registered-user",
                TestContext.Current.CancellationToken);

            // Assert
            result.Success.Should().BeTrue();
            result.ProfilesLinked.Should().Be(2);
            first.UserId.Should().Be("registered-user");
            second.UserId.Should().Be("registered-user");
            _profiles.Verify(repository => repository.UpdateAsync(first), Times.Once);
            _profiles.Verify(repository => repository.UpdateAsync(second), Times.Once);
        }

        private VerificationCodeClientAdapter CreateAdapter()
            => new(_verificationCodes.Object, _profiles.Object, _users.Object);

        private static UserProfile CreateProfile(string userId)
            => UserProfile.Create(userId, "Jan", "Kowalski", false, null, null, "guest@test.com", "123456789");
    }
}
