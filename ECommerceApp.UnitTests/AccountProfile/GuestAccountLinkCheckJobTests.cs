using ECommerceApp.Application.AccountProfile.Contracts;
using ECommerceApp.Application.AccountProfile.Handlers;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.AccountProfile;
using AwesomeAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.AccountProfile
{
    public class GuestAccountLinkCheckJobTests
    {
        private readonly Mock<IUserProfileRepository> _profiles = new();
        private readonly Mock<IGuestAccountProvisioner> _users = new();
        private readonly Mock<IVerificationCodeClient> _codes = new();

        [Fact]
        public async Task ExecuteAsync_NoMatchingProfiles_DoesNotGenerateCode()
        {
            _profiles.Setup(repository => repository.GetByEmailAsync("guest@test.com", false))
                .ReturnsAsync(new List<UserProfile>());

            var context = await ExecuteAsync("guest@test.com");

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _codes.Verify(client => client.RequestGuestAccountLinkAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_OneUnclaimedProfile_GeneratesCodeForEmail()
        {
            var profile = CreateProfile("gst_1");
            SetupCandidates(profile);

            await ExecuteAsync("guest@test.com");

            _codes.Verify(client => client.RequestGuestAccountLinkAsync("guest@test.com", It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_MultipleUnclaimedProfiles_GeneratesOneCode()
        {
            var first = CreateProfile("gst_1");
            var second = CreateProfile("gst_2");
            SetupCandidates(first, second);

            await ExecuteAsync("guest@test.com");

            _codes.Verify(client => client.RequestGuestAccountLinkAsync("guest@test.com", It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        private async Task<JobExecutionContext> ExecuteAsync(string email)
        {
            _users.Setup(users => users.GetRegisteredUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>()))
                .ReturnsAsync(new HashSet<string>());
            var context = new JobExecutionContext(email, "execution-1");
            await new GuestAccountLinkCheckJob(_profiles.Object, _users.Object, _codes.Object)
                .ExecuteAsync(context, TestContext.Current.CancellationToken);
            return context;
        }

        private void SetupCandidates(params UserProfile[] profiles)
        {
            _profiles.Setup(repository => repository.GetByEmailAsync("guest@test.com", false))
                .ReturnsAsync(new List<UserProfile>(profiles));
        }

        private static UserProfile CreateProfile(string userId)
            => UserProfile.Create(userId, "Jan", "Kowalski", false, null, null, "guest@test.com", "123456789");
    }
}
