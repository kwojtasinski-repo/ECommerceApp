using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Supporting.TimeManagement;
using AwesomeAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Backoffice
{
    public class BackofficeJobServiceTests
    {
        private readonly Mock<IJobManagementService> _jobManagement;

        public BackofficeJobServiceTests()
        {
            _jobManagement = new Mock<IJobManagementService>();
        }

        private IBackofficeJobService CreateSut() => new BackofficeJobService(_jobManagement.Object);

        private static JobStatusSummary MakeJob(string name, bool enabled, DateTime? lastRun = null, DateTime? nextRun = null)
            => new JobStatusSummary
            {
                JobName = name,
                Schedule = "0 * * * *",
                IsEnabled = enabled,
                LastRunAt = lastRun,
                NextRunAt = nextRun,
                NeverRun = lastRun == null
            };

        // ── GetJobsAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetJobsAsync_WithJobs_AppliesPagingAndMapsStatus()
        {
            // Arrange — 3 jobs, page 1 with size 2
            var now = DateTime.UtcNow;
            _jobManagement
                .Setup(s => s.GetAllJobsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JobStatusSummary>
                {
                    MakeJob("JobA", enabled: true,  lastRun: now.AddHours(-1), nextRun: now.AddHours(1)),
                    MakeJob("JobB", enabled: false, lastRun: now.AddHours(-2)),
                    MakeJob("JobC", enabled: true)
                });

            // Act
            var result = await CreateSut().GetJobsAsync(pageSize: 2, pageNo: 1);

            // Assert
            result.TotalCount.Should().Be(3);
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(2);
            result.Jobs.Should().HaveCount(2);

            result.Jobs[0].Name.Should().Be("JobA");
            result.Jobs[0].Status.Should().Be("Enabled");
            result.Jobs[1].Name.Should().Be("JobB");
            result.Jobs[1].Status.Should().Be("Disabled");
        }

        [Fact]
        public async Task GetJobsAsync_PageTwo_ReturnsCorrectSlice()
        {
            // Arrange — 3 jobs, request page 2 with size 2
            _jobManagement
                .Setup(s => s.GetAllJobsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JobStatusSummary>
                {
                    MakeJob("JobA", true),
                    MakeJob("JobB", false),
                    MakeJob("JobC", true)
                });

            // Act
            var result = await CreateSut().GetJobsAsync(pageSize: 2, pageNo: 2);

            // Assert
            result.Jobs.Should().HaveCount(1);
            result.Jobs[0].Name.Should().Be("JobC");
        }

        [Fact]
        public async Task GetJobsAsync_EmptyList_ReturnsEmptyVm()
        {
            // Arrange
            _jobManagement
                .Setup(s => s.GetAllJobsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JobStatusSummary>());

            // Act
            var result = await CreateSut().GetJobsAsync(10, 1);

            // Assert
            result.Jobs.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        // ── GetJobDetailAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetJobDetailAsync_ExistingJob_ReturnsMappedVm()
        {
            // Arrange
            var now = DateTime.UtcNow;
            _jobManagement
                .Setup(s => s.GetAllJobsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JobStatusSummary>
                {
                    new JobStatusSummary
                    {
                        JobName = "TargetJob",
                        Schedule = "0 0 * * *",
                        IsEnabled = true,
                        LastRunAt = now.AddDays(-1),
                        NextRunAt = now.AddDays(1)
                    }
                });

            // Act
            var result = await CreateSut().GetJobDetailAsync("TargetJob");

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("TargetJob");
            result.Status.Should().Be("Enabled");
            result.CronExpression.Should().Be("0 0 * * *");
        }

        [Fact]
        public async Task GetJobDetailAsync_NotFound_ReturnsNull()
        {
            // Arrange
            _jobManagement
                .Setup(s => s.GetAllJobsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JobStatusSummary> { MakeJob("OtherJob", true) });

            // Act
            var result = await CreateSut().GetJobDetailAsync("MissingJob");

            // Assert
            result.Should().BeNull();
        }

        // ── GetJobHistoryAsync ────────────────────────────────────────────────

        [Fact]
        public async Task GetJobHistoryAsync_WithHistory_ReturnsMappedVm()
        {
            // Arrange
            var now = DateTime.UtcNow;
            _jobManagement
                .Setup(s => s.GetAllHistoryAsync(1, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JobExecutionRecord>
                {
                    new() { JobName = "JobA", ExecutionId = "exec-1", StartedAt = now.AddMinutes(-10), CompletedAt = now.AddMinutes(-9), Succeeded = true  },
                    new() { JobName = "JobB", ExecutionId = "exec-2", StartedAt = now.AddMinutes(-5),  CompletedAt = now.AddMinutes(-4), Succeeded = false }
                });
            _jobManagement
                .Setup(s => s.GetAllHistoryCountAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            // Act
            var result = await CreateSut().GetJobHistoryAsync(10, 1);

            // Assert
            result.TotalCount.Should().Be(2);
            result.CurrentPage.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.Jobs.Should().HaveCount(2);

            result.Jobs[0].Name.Should().Be("JobA");
            result.Jobs[0].Status.Should().Be("Succeeded");
            result.Jobs[1].Name.Should().Be("JobB");
            result.Jobs[1].Status.Should().Be("Failed");
        }

        [Fact]
        public async Task GetJobHistoryAsync_EmptyHistory_ReturnsEmptyVm()
        {
            // Arrange
            _jobManagement
                .Setup(s => s.GetAllHistoryAsync(1, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<JobExecutionRecord>());
            _jobManagement
                .Setup(s => s.GetAllHistoryCountAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            // Act
            var result = await CreateSut().GetJobHistoryAsync(10, 1);

            // Assert
            result.Jobs.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }
    }
}
