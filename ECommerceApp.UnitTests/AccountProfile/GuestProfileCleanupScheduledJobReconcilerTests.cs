using ECommerceApp.Application.AccountProfile.Options;
using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Infrastructure.AccountProfile;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AwesomeAssertions;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.AccountProfile
{
    public class GuestProfileCleanupScheduledJobReconcilerTests
    {
        [Fact]
        public async Task ReconcileAsync_JobMissing_CreatesWithConfigScheduleAndEnabledState()
        {
            var repository = new Mock<IScheduledJobRepository>();
            repository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ScheduledJob)null);
            var options = new GuestProfileCleanupOptions { Enabled = false, Schedule = "15 4 * * *" };
            var reconciler = CreateReconciler(repository, options);

            await reconciler.ReconcileAsync(CancellationToken.None);

            repository.Verify(r => r.AddAsync(It.Is<ScheduledJob>(job =>
                job.Name.Value == "UnclaimedGuestProfileCleanup" && job.Schedule.Value == "15 4 * * *" && !job.IsEnabled), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReconcileAsync_JobExistsWithDifferentSchedule_UpdatesSchedule()
        {
            var repository = new Mock<IScheduledJobRepository>();
            var job = ScheduledJob.Create("UnclaimedGuestProfileCleanup", "0 4 * * *", null, 3);
            repository.Setup(r => r.GetByNameAsync("UnclaimedGuestProfileCleanup", It.IsAny<CancellationToken>())).ReturnsAsync(job);
            var options = new GuestProfileCleanupOptions { Schedule = "45 5 * * *" };
            var reconciler = CreateReconciler(repository, options);

            await reconciler.ReconcileAsync(CancellationToken.None);

            job.Schedule.Value.Should().Be("45 5 * * *");
            repository.Verify(r => r.UpdateAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReconcileAsync_JobMatchesConfig_NoOpUpdate()
        {
            var repository = new Mock<IScheduledJobRepository>();
            var job = ScheduledJob.Create("UnclaimedGuestProfileCleanup", "0 4 * * *", null, 3);
            repository.Setup(r => r.GetByNameAsync("UnclaimedGuestProfileCleanup", It.IsAny<CancellationToken>())).ReturnsAsync(job);
            var reconciler = CreateReconciler(repository, new GuestProfileCleanupOptions());

            await reconciler.ReconcileAsync(CancellationToken.None);

            repository.Verify(r => r.UpdateAsync(It.IsAny<ScheduledJob>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ReconcileAsync_MalformedCronInConfig_LogsAndDoesNotCrashStartup()
        {
            var repository = new Mock<IScheduledJobRepository>();
            repository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ScheduledJob)null);
            var reconciler = CreateReconciler(repository, new GuestProfileCleanupOptions { Schedule = "invalid" });

            await reconciler.ReconcileAsync(CancellationToken.None);

            repository.Verify(r => r.AddAsync(It.Is<ScheduledJob>(job => job.Schedule.Value == "0 4 * * *"), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static GuestProfileCleanupScheduledJobReconciler CreateReconciler(
            Mock<IScheduledJobRepository> repository,
            GuestProfileCleanupOptions options)
        {
            var services = new ServiceCollection()
                .AddScoped<IScheduledJobRepository>(_ => repository.Object)
                .AddLogging()
                .BuildServiceProvider();
            return new GuestProfileCleanupScheduledJobReconciler(
                services.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(options),
                services.GetRequiredService<ILogger<GuestProfileCleanupScheduledJobReconciler>>());
        }
    }
}
