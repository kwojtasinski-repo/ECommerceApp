using ECommerceApp.Application.AccountProfile.Results;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Domain.AccountProfile;
using AwesomeAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.AccountProfile
{
    public class GuestPromotionServiceTests
    {
        private readonly Mock<IUserProfileRepository> _profiles = new();
        private readonly Mock<IGuestAccountProvisioner> _users = new();

        private GuestPromotionService CreateService() => new(_profiles.Object, _users.Object);

        private void SetupGuestIsNotRegistered(string userId)
        {
            _users.Setup(u => u.IsRegisteredAsync(userId)).ReturnsAsync(false);
        }

        private void SetupSuccessfulGuestRegistration(string userId)
        {
            SetupGuestIsNotRegistered(userId);
            _users.Setup(u => u.CreateAsync("jan@test.com", "Password1!"))
                .ReturnsAsync(new GuestAccountProvisioningResult("registered-1", new List<string>()));
        }

        private void SetupFailedGuestRegistration(string userId)
        {
            SetupGuestIsNotRegistered(userId);
            _users.Setup(u => u.CreateAsync("jan@test.com", "Password1!"))
                .ReturnsAsync(new GuestAccountProvisioningResult(null, new List<string> { "weak password" }));
        }

        private void SetupProfileMissing(int profileId)
        {
            _profiles.Setup(r => r.GetByIdAsync(new UserProfileId(profileId), true))
                .ReturnsAsync((UserProfile)null);
        }

        private void SetupGuestAlreadyRegistered(string userId)
        {
            _users.Setup(u => u.IsRegisteredAsync(userId)).ReturnsAsync(true);
        }

        [Fact]
        public async Task PromoteAsync_RequestingUserIdDoesNotMatchProfileOwner_ReturnsNotOwner()
        {
            // Arrange
            var profile = CreateProfile("gst_owner");
            SetupProfile(profile, 5);

            // Act
            var result = await CreateService().PromoteAsync(5, "gst_attacker", "Password1!");

            // Assert
            result.Status.Should().Be(PromotionStatus.NotOwner);
            _users.Verify(u => u.CreateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PromoteAsync_ProfileNotFound_ReturnsProfileNotFound()
        {
            // Arrange
            SetupProfileMissing(99);

            // Act
            var result = await CreateService().PromoteAsync(99, "gst_owner", "Password1!");

            // Assert
            result.Status.Should().Be(PromotionStatus.ProfileNotFound);
        }

        [Fact]
        public async Task PromoteAsync_Valid_CreatesApplicationUserAndReassignsOwner()
        {
            // Arrange
            var profile = CreateProfile("gst_owner");
            SetupProfile(profile, 5);
            SetupSuccessfulGuestRegistration("gst_owner");

            // Act
            var result = await CreateService().PromoteAsync(5, "gst_owner", "Password1!");

            // Assert
            result.Status.Should().Be(PromotionStatus.Success);
            profile.UserId.Should().NotBe("gst_owner");
            _profiles.Verify(r => r.UpdateAsync(profile), Times.Once);
        }

        [Fact]
        public async Task PromoteAsync_IdentityCreationFails_DoesNotReassignOwner()
        {
            // Arrange
            var profile = CreateProfile("gst_owner");
            SetupProfile(profile, 5);
            SetupFailedGuestRegistration("gst_owner");

            // Act
            var result = await CreateService().PromoteAsync(5, "gst_owner", "Password1!");

            // Assert
            result.Status.Should().Be(PromotionStatus.IdentityCreationFailed);
            profile.UserId.Should().Be("gst_owner");
            _profiles.Verify(r => r.UpdateAsync(It.IsAny<UserProfile>()), Times.Never);
        }

        [Fact]
        public async Task PromoteAsync_ProfileAlreadyRegistered_ReturnsAlreadyRegistered()
        {
            // Arrange
            var profile = CreateProfile("registered-1");
            SetupProfile(profile, 5);
            SetupGuestAlreadyRegistered("registered-1");

            // Act
            var result = await CreateService().PromoteAsync(5, "registered-1", "Password1!");

            // Assert
            result.Status.Should().Be(PromotionStatus.AlreadyRegistered);
            _users.Verify(u => u.CreateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        private void SetupProfile(UserProfile profile, int id)
        {
            EntityIdSetter.Set(profile, new UserProfileId(id));
            _profiles.Setup(r => r.GetByIdAsync(new UserProfileId(id), true)).ReturnsAsync(profile);
        }

        private static UserProfile CreateProfile(string userId)
            => UserProfile.Create(userId, "Jan", "Kowalski", false, null, null, "jan@test.com", "123456789");
    }
}