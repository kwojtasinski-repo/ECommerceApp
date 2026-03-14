using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Presale.Checkout;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class SoftReservationExpiredJobTests : IDisposable
    {
        private readonly Mock<ISoftReservationRepository> _reservationRepo;
        private readonly IMemoryCache _cache;
        private readonly SoftReservationExpiredJob _job;

        public SoftReservationExpiredJobTests()
        {
            _reservationRepo = new Mock<ISoftReservationRepository>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _job = new SoftReservationExpiredJob(_reservationRepo.Object, _cache);
        }

        public void Dispose() => _cache.Dispose();

        [Fact]
        public void TaskName_ShouldMatchJobTaskNameConstant()
        {
            _job.TaskName.Should().Be(SoftReservationExpiredJob.JobTaskName);
        }

        [Fact]
        public async Task ExecuteAsync_MissingEntityId_ShouldReportFailure()
        {
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            await _job.ExecuteAsync(context, default);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>()
                .Which.Error.Should().Contain("Missing EntityId");
        }

        [Fact]
        public async Task ExecuteAsync_InvalidEntityId_ShouldReportFailure()
        {
            var context = new JobExecutionContext("not-an-int", Guid.NewGuid().ToString());

            await _job.ExecuteAsync(context, default);

            context.Outcome.Should().BeOfType<JobOutcome.Failure>()
                .Which.Error.Should().Contain("not-an-int");
        }

        [Fact]
        public async Task ExecuteAsync_ReservationNotFound_ShouldReportSuccessNoOp()
        {
            _reservationRepo.Setup(r => r.GetByIdAsync(It.IsAny<SoftReservationId>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync((SoftReservation)null!);
            var context = new JobExecutionContext("42", Guid.NewGuid().ToString());

            await _job.ExecuteAsync(context, default);

            context.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("No-op");
            _reservationRepo.Verify(r => r.DeleteAsync(It.IsAny<SoftReservation>(), It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ReservationFound_ShouldDeleteFromDbAndEvictCacheAndReportSuccess()
        {
            var reservation = SoftReservation.Create(1, "user-1", 2, 10m, DateTime.UtcNow.AddMinutes(15));
            _cache.Set("sr:1:user-1", reservation, TimeSpan.FromMinutes(15));

            _reservationRepo.Setup(r => r.GetByIdAsync(It.IsAny<SoftReservationId>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(reservation);
            var context = new JobExecutionContext("1", Guid.NewGuid().ToString());

            await _job.ExecuteAsync(context, default);

            _reservationRepo.Verify(r => r.DeleteAsync(reservation, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
            _cache.TryGetValue("sr:1:user-1", out _).Should().BeFalse();
            context.Outcome.Should().BeOfType<JobOutcome.Success>();
        }

        [Fact]
        public async Task ExecuteAsync_MissingEntityId_ShouldNotCallDeleteAsync()
        {
            var context = new JobExecutionContext(null, Guid.NewGuid().ToString());

            await _job.ExecuteAsync(context, default);

            _reservationRepo.Verify(r => r.DeleteAsync(It.IsAny<SoftReservation>(), It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_CommittedReservation_ShouldSkipAndReportSuccessNoOp()
        {
            var reservation = SoftReservation.Create(1, "user-1", 2, 10m, DateTime.UtcNow.AddMinutes(15));
            reservation.Commit();

            _reservationRepo.Setup(r => r.GetByIdAsync(It.IsAny<SoftReservationId>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(reservation);
            var context = new JobExecutionContext("1", Guid.NewGuid().ToString());

            await _job.ExecuteAsync(context, default);

            _reservationRepo.Verify(r => r.DeleteAsync(It.IsAny<SoftReservation>(), It.IsAny<System.Threading.CancellationToken>()), Times.Never);
            context.Outcome.Should().BeOfType<JobOutcome.Success>()
                .Which.Message.Should().Contain("No-op");
        }
    }
}
