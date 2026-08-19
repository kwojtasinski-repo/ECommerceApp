using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AwesomeAssertions;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Messaging
{
    public class MessagingScheduledJobReconcilerTests
    {
        [Fact]
        public async Task ReconcileAsync_JobMissing_CreatesWithConfigScheduleAndEnabledState()
        {
            // Arrange
            var repository = new Mock<IScheduledJobRepository>();
            SetupMissingJobs(repository);
            var options = new MessagingOptions { CleanupEnabled = false, OutboxCleanupSchedule = "15 4 * * *" };
            var reconciler = CreateReconciler(repository, options);

            // Act
            await reconciler.ReconcileAsync(CancellationToken.None);

            // Assert
            repository.Verify(r => r.AddAsync(It.Is<ScheduledJob>(job =>
                job.Name.Value == "OutboxCleanup" && job.Schedule.Value == "15 4 * * *" && !job.IsEnabled), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReconcileAsync_JobExistsWithDifferentSchedule_UpdatesSchedule()
        {
            // Arrange
            var repository = new Mock<IScheduledJobRepository>();
            var job = ScheduledJob.Create("OutboxCleanup", "0 3 * * *", null, 3);
            SetupExistingOutboxJob(repository, job);
            var options = new MessagingOptions { OutboxCleanupSchedule = "45 5 * * *" };
            var reconciler = CreateReconciler(repository, options);

            // Act
            await reconciler.ReconcileAsync(CancellationToken.None);

            // Assert
            job.Schedule.Value.Should().Be("45 5 * * *");
            repository.Verify(r => r.UpdateAsync(job, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReconcileAsync_JobMatchesConfig_NoOpUpdate()
        {
            // Arrange
            var repository = new Mock<IScheduledJobRepository>();
            SetupJobsMatchingConfig(repository);
            var reconciler = CreateReconciler(repository, new MessagingOptions());

            // Act
            await reconciler.ReconcileAsync(CancellationToken.None);

            // Assert
            repository.Verify(r => r.UpdateAsync(It.IsAny<ScheduledJob>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ReconcileAsync_MalformedCronInConfig_LogsAndDoesNotCrashStartup()
        {
            // Arrange
            var repository = new Mock<IScheduledJobRepository>();
            SetupMissingJobs(repository);
            var reconciler = CreateReconciler(repository, new MessagingOptions { OutboxCleanupSchedule = "invalid" });

            // Act
            await reconciler.ReconcileAsync(CancellationToken.None);

            // Assert
            repository.Verify(r => r.AddAsync(It.Is<ScheduledJob>(job => job.Schedule.Value == "0 3 * * *"), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static void SetupMissingJobs(Mock<IScheduledJobRepository> repository)
        {
            repository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ScheduledJob)null);
        }

        private static void SetupExistingOutboxJob(Mock<IScheduledJobRepository> repository, ScheduledJob job)
        {
            repository.Setup(r => r.GetByNameAsync("OutboxCleanup", It.IsAny<CancellationToken>()))
                .ReturnsAsync(job);
            repository.Setup(r => r.GetByNameAsync("InboxCleanup", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ScheduledJob)null);
        }

        private static void SetupJobsMatchingConfig(Mock<IScheduledJobRepository> repository)
        {
            repository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string name, CancellationToken _) => ScheduledJob.Create(
                    name,
                    name == "OutboxCleanup" ? "0 3 * * *" : "30 3 * * *",
                    null,
                    3));
        }

        private static MessagingScheduledJobReconciler CreateReconciler(
            Mock<IScheduledJobRepository> repository,
            MessagingOptions options)
        {
            var services = new ServiceCollection()
                .AddScoped<IScheduledJobRepository>(_ => repository.Object)
                .AddSingleton(options)
                .AddLogging()
                .BuildServiceProvider();
            return new MessagingScheduledJobReconciler(
                services.GetRequiredService<IServiceScopeFactory>(),
                options,
                services.GetRequiredService<ILogger<MessagingScheduledJobReconciler>>());
        }
    }
}