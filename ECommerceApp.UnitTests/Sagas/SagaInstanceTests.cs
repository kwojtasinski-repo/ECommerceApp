using AwesomeAssertions;
using ECommerceApp.Domain.Sagas;
using ECommerceApp.Domain.Shared;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Sagas
{
    public class SagaInstanceTests
    {
        [Fact]
        public void Create_SetsRunningStatusAndCreationData()
        {
            var instance = SagaInstance.Create("OrderPlacement", "order-123");

            instance.SagaType.Should().Be("OrderPlacement");
            instance.CorrelationId.Should().Be("order-123");
            instance.Status.Should().Be(SagaInstanceStatus.Running);
            instance.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
            instance.CompletedAt.Should().BeNull();
        }

        [Fact]
        public void MarkCompleted_FromRunning_SetsCompletedStatusAndTimestamp()
        {
            var instance = SagaInstance.Create("OrderPlacement", "order-123");

            instance.MarkCompleted();

            instance.Status.Should().Be(SagaInstanceStatus.Completed);
            instance.CompletedAt.Should().NotBeNull();
        }

        [Fact]
        public void MarkCompensating_FromRunning_SetsCompensatingStatus()
        {
            var instance = SagaInstance.Create("OrderPlacement", "order-123");

            instance.MarkCompensating();

            instance.Status.Should().Be(SagaInstanceStatus.Compensating);
        }

        [Fact]
        public void MarkFailed_FromRunning_SetsFailedStatusAndTimestamp()
        {
            var instance = SagaInstance.Create("OrderPlacement", "order-123");

            instance.MarkFailed();

            instance.Status.Should().Be(SagaInstanceStatus.Failed);
            instance.CompletedAt.Should().NotBeNull();
        }

        [Fact]
        public void MarkFailed_FromCompensating_SetsFailedStatus()
        {
            var instance = SagaInstance.Create("OrderPlacement", "order-123");
            instance.MarkCompensating();

            instance.MarkFailed();

            instance.Status.Should().Be(SagaInstanceStatus.Failed);
        }

        [Fact]
        public void Create_WithEmptySagaType_Throws()
        {
            Action act = () => SagaInstance.Create("", "order-123");

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Create_WithEmptyCorrelationId_Throws()
        {
            Action act = () => SagaInstance.Create("OrderPlacement", "");

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void MarkCompleted_FromCompensating_Throws()
        {
            var instance = SagaInstance.Create("OrderPlacement", "order-123");
            instance.MarkCompensating();

            Action act = instance.MarkCompleted;

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void MarkCompensating_FromCompleted_Throws()
        {
            var instance = SagaInstance.Create("OrderPlacement", "order-123");
            instance.MarkCompleted();

            Action act = instance.MarkCompensating;

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void MarkFailed_FromCompleted_Throws()
        {
            var instance = SagaInstance.Create("OrderPlacement", "order-123");
            instance.MarkCompleted();

            Action act = instance.MarkFailed;

            act.Should().Throw<DomainException>();
        }
    }
}