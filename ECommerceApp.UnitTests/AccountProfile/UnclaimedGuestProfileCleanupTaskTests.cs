using ECommerceApp.Application.AccountProfile.Options;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.AccountProfile;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.AccountProfile
{
    public class UnclaimedGuestProfileCleanupTaskTests
    {
        private readonly Mock<IUserProfileRepository> _profiles;
        private readonly Mock<IGuestAccountProvisioner> _accountProvisioner;
        private readonly Mock<IModuleClient> _moduleClient;
        private GuestProfileCleanupOptions _options;

        public UnclaimedGuestProfileCleanupTaskTests()
        {
            _profiles = new Mock<IUserProfileRepository>();
            _accountProvisioner = new Mock<IGuestAccountProvisioner>();
            _moduleClient = new Mock<IModuleClient>();
            _options = new GuestProfileCleanupOptions { Enabled = true, RetentionDays = 90 };
        }

        private UnclaimedGuestProfileCleanupTask CreateTask() => new(
            _profiles.Object,
            _accountProvisioner.Object,
            _moduleClient.Object,
            Options.Create(_options));

        private static UserProfile NewProfile(string userId, int id = 1)
        {
            var profile = UserProfile.Create(userId, "Jan", "Kowalski", false, null, null, "jan@example.com", "500600700");
            typeof(UserProfile).GetProperty(nameof(UserProfile.Id))!.SetValue(profile, new UserProfileId(id));
            return profile;
        }

        [Fact]
        public void TaskName_ShouldBeUnclaimedGuestProfileCleanup()
        {
            CreateTask().TaskName.Should().Be("UnclaimedGuestProfileCleanup");
        }

        [Fact]
        public async Task ExecuteAsync_UnclaimedProfileWithAnOrder_DoesNotDeleteIt()
        {
            var profile = NewProfile("gst_1");
            _profiles.Setup(r => r.GetOlderThanAsync(It.IsAny<System.DateTime>()))
                .ReturnsAsync(new List<UserProfile> { profile });
            _accountProvisioner.Setup(a => a.GetRegisteredUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>()))
                .ReturnsAsync(new HashSet<string>());
            _moduleClient.Setup(m => m.SendAsync(It.IsAny<CustomersWithOrdersQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlySet<int>)new HashSet<int> { profile.Id.Value });
            var context = new JobExecutionContext(null, "exec-1");

            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>().Which.Message.Should().Contain("0");
            _profiles.Verify(r => r.DeleteAsync(It.IsAny<UserProfileId>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_UnclaimedProfileOlderThanThreshold_DeletesIt()
        {
            var profile = NewProfile("gst_2");
            _profiles.Setup(r => r.GetOlderThanAsync(It.IsAny<System.DateTime>()))
                .ReturnsAsync(new List<UserProfile> { profile });
            _accountProvisioner.Setup(a => a.GetRegisteredUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>()))
                .ReturnsAsync(new HashSet<string>());
            _moduleClient.Setup(m => m.SendAsync(It.IsAny<CustomersWithOrdersQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlySet<int>)new HashSet<int>());
            _profiles.Setup(r => r.DeleteAsync(profile.Id)).ReturnsAsync(true);
            var context = new JobExecutionContext(null, "exec-2");

            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>().Which.Message.Should().Contain("1");
            _profiles.Verify(r => r.DeleteAsync(profile.Id), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ClaimedProfile_DoesNotDeleteIt()
        {
            var profile = NewProfile("real-user-id");
            _profiles.Setup(r => r.GetOlderThanAsync(It.IsAny<System.DateTime>()))
                .ReturnsAsync(new List<UserProfile> { profile });
            _accountProvisioner.Setup(a => a.GetRegisteredUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>()))
                .ReturnsAsync(new HashSet<string> { "real-user-id" });
            var context = new JobExecutionContext(null, "exec-3");

            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _profiles.Verify(r => r.DeleteAsync(It.IsAny<UserProfileId>()), Times.Never);
            _moduleClient.Verify(m => m.SendAsync(It.IsAny<CustomersWithOrdersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ProfileNewerThanThreshold_DoesNotDeleteIt()
        {
            _profiles.Setup(r => r.GetOlderThanAsync(It.IsAny<System.DateTime>()))
                .ReturnsAsync(new List<UserProfile>());
            var context = new JobExecutionContext(null, "exec-4");

            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _profiles.Verify(r => r.DeleteAsync(It.IsAny<UserProfileId>()), Times.Never);
            _accountProvisioner.Verify(a => a.GetRegisteredUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_RepositoryThrows_ReportsFailureNotException()
        {
            _profiles.Setup(r => r.GetOlderThanAsync(It.IsAny<System.DateTime>()))
                .ThrowsAsync(new System.Exception("DB connection failed"));
            var context = new JobExecutionContext(null, "exec-5");

            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>()
                .Which.Error.Should().Contain("DB connection failed");
        }

        [Fact]
        public async Task ExecuteAsync_Disabled_ReportsSuccessAndDeletesNothing()
        {
            _options = new GuestProfileCleanupOptions { Enabled = false, RetentionDays = 90 };
            var context = new JobExecutionContext(null, "exec-6");

            await CreateTask().ExecuteAsync(context, CancellationToken.None);

            context.Outcome.Should().BeOfType<JobOutcome.Success>();
            _profiles.Verify(r => r.GetOlderThanAsync(It.IsAny<System.DateTime>()), Times.Never);
        }
    }
}
