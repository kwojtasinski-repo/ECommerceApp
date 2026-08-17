using AwesomeAssertions;
using ECommerceApp.Domain.Sagas;
using ECommerceApp.Domain.Shared;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Sagas
{
    public class SagaStepTests
    {
        [Fact]
        public void Create_SetsPendingStatusAndData()
        {
            var step = SagaStep.Create(42, "ReserveStock", "{}");

            step.SagaInstanceId.Should().Be(42);
            step.StepName.Should().Be("ReserveStock");
            step.Payload.Should().Be("{}");
            step.Status.Should().Be(SagaStepStatus.Pending);
            step.OccurredAt.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void MarkCompleted_FromPending_SetsCompletedStatus()
        {
            var step = SagaStep.Create(42, "ReserveStock", "{}");

            step.MarkCompleted();

            step.Status.Should().Be(SagaStepStatus.Completed);
        }

        [Fact]
        public void MarkFailed_FromPending_SetsFailedStatus()
        {
            var step = SagaStep.Create(42, "ReserveStock", "{}");

            step.MarkFailed();

            step.Status.Should().Be(SagaStepStatus.Failed);
        }

        [Fact]
        public void MarkCompensated_FromCompleted_SetsCompensatedStatus()
        {
            var step = SagaStep.Create(42, "ReserveStock", "{}");
            step.MarkCompleted();

            step.MarkCompensated();

            step.Status.Should().Be(SagaStepStatus.Compensated);
        }

        [Fact]
        public void Create_WithInvalidArguments_Throws()
        {
            Action invalidId = () => SagaStep.Create(0, "ReserveStock", "{}");
            Action invalidName = () => SagaStep.Create(42, "", "{}");
            Action invalidPayload = () => SagaStep.Create(42, "ReserveStock", null!);

            invalidId.Should().Throw<DomainException>();
            invalidName.Should().Throw<DomainException>();
            invalidPayload.Should().Throw<DomainException>();
        }

        [Fact]
        public void MarkCompleted_FromFailed_Throws()
        {
            var step = SagaStep.Create(42, "ReserveStock", "{}");
            step.MarkFailed();

            Action act = step.MarkCompleted;

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void MarkFailed_FromCompleted_Throws()
        {
            var step = SagaStep.Create(42, "ReserveStock", "{}");
            step.MarkCompleted();

            Action act = step.MarkFailed;

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void MarkCompensated_FromPending_Throws()
        {
            var step = SagaStep.Create(42, "ReserveStock", "{}");

            Action act = step.MarkCompensated;

            act.Should().Throw<DomainException>();
        }
    }
}