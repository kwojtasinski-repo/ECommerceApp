using AwesomeAssertions;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sagas;
using ECommerceApp.Domain.Sagas;
using ECommerceApp.Infrastructure.Sagas;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sagas
{
    public class SagaTransitionHandlerTests
    {
        [Fact]
        public async Task SuccessTransition_CreatesSagaAndCompletesStep()
        {
            var transaction = new Mock<IOutboxTransaction>();
            var unitOfWork = new Mock<ISagaUnitOfWork>();
            unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);

            var repository = new Mock<ISagaRepository>();
            SagaInstance? createdInstance = null;
            SagaStep? createdStep = null;
            repository.Setup(x => x.FindRunningAsync("TestSaga", "order-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaInstance?)null);
            repository.Setup(x => x.AddAsync(It.IsAny<SagaInstance>(), It.IsAny<CancellationToken>()))
                .Callback<SagaInstance, CancellationToken>((instance, _) =>
                {
                    SetId(instance, 7);
                    createdInstance = instance;
                })
                .Returns(Task.CompletedTask);
            repository.Setup(x => x.FindStepAsync(7, "ReserveStock", It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaStep?)null);
            repository.Setup(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()))
                .Callback<SagaStep, CancellationToken>((step, _) => createdStep = step)
                .Returns(Task.CompletedTask);

            var guard = new Mock<IProcessedMessageGuard>();
            guard.Setup(x => x.TryMarkProcessedAsync(
                    10,
                    It.IsAny<string>(),
                    transaction.Object,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var outboxWriter = new Mock<IOutboxWriter>();
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "ReserveStock", SagaTransitionKind.Success, true),
                    null),
                unitOfWork,
                repository,
                outboxWriter,
                guard,
                new TestPayloadSerializer());

            await handler.HandleAsync(new TestMessage("order-123"), 10);

            createdInstance.Should().NotBeNull();
            createdStep.Should().NotBeNull();
            createdStep!.Status.Should().Be(SagaStepStatus.Completed);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Redelivery_WhenGuardRejects_IsNoOp()
        {
            var transaction = new Mock<IOutboxTransaction>();
            var unitOfWork = new Mock<ISagaUnitOfWork>();
            unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);
            var repository = new Mock<ISagaRepository>();
            var guard = new Mock<IProcessedMessageGuard>();
            guard.Setup(x => x.TryMarkProcessedAsync(
                    10,
                    It.IsAny<string>(),
                    transaction.Object,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "ReserveStock", SagaTransitionKind.Success, true),
                    null),
                unitOfWork,
                repository,
                new Mock<IOutboxWriter>(),
                guard,
                new TestPayloadSerializer());

            await handler.HandleAsync(new TestMessage("order-123"), 10);

            repository.Verify(x => x.FindRunningAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task FailureTransition_MarksSagaCompensatingAndEnqueuesCompensation()
        {
            var transaction = new Mock<IOutboxTransaction>();
            var unitOfWork = new Mock<ISagaUnitOfWork>();
            unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = new Mock<ISagaRepository>();
            SagaStep? createdStep = null;
            repository.Setup(x => x.FindRunningAsync("TestSaga", "order-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(saga);
            repository.Setup(x => x.FindStepAsync(7, "PaymentFailed", It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaStep?)null);
            repository.Setup(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()))
                .Callback<SagaStep, CancellationToken>((step, _) => createdStep = step)
                .Returns(Task.CompletedTask);

            var guard = new Mock<IProcessedMessageGuard>();
            guard.Setup(x => x.TryMarkProcessedAsync(
                    10,
                    It.IsAny<string>(),
                    transaction.Object,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var outboxWriter = new Mock<IOutboxWriter>();
            var compensation = new TestCompensation("order-123");
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "PaymentFailed", SagaTransitionKind.Failure, false),
                    context =>
                    {
                        context.Get<TestMessage>("PaymentFailed").CorrelationId.Should().Be("order-123");
                        return compensation;
                    }),
                unitOfWork,
                repository,
                outboxWriter,
                guard,
                new TestPayloadSerializer());

            await handler.HandleAsync(new TestMessage("order-123"), 10);

            saga.Status.Should().Be(SagaInstanceStatus.Compensating);
            createdStep!.Status.Should().Be(SagaStepStatus.Failed);
            outboxWriter.Verify(x => x.EnqueueAsync(compensation, transaction.Object, It.IsAny<CancellationToken>()), Times.Once);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SuccessTransition_StepDoesNotStartNewInstance_WhenNoRunningSaga_IsSkipped()
        {
            var transaction = new Mock<IOutboxTransaction>();
            var unitOfWork = new Mock<ISagaUnitOfWork>();
            unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);
            var repository = new Mock<ISagaRepository>();
            repository.Setup(x => x.FindRunningAsync("TestSaga", "order-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaInstance?)null);
            var guard = new Mock<IProcessedMessageGuard>();
            guard.Setup(x => x.TryMarkProcessedAsync(
                    10, It.IsAny<string>(), transaction.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "ReturnStock", SagaTransitionKind.Success, false),
                    null),
                unitOfWork,
                repository,
                new Mock<IOutboxWriter>(),
                guard,
                new TestPayloadSerializer());

            await handler.HandleAsync(new TestMessage("order-123"), 10);

            // A non-starting step arriving with no running instance (e.g. out-of-order delivery)
            // must not spuriously create one - regardless of whether the step is Success or Failure
            // kind. See SagaTransitionHandler.cs's saga-is-null branch.
            repository.Verify(x => x.AddAsync(It.IsAny<SagaInstance>(), It.IsAny<CancellationToken>()), Times.Never);
            repository.Verify(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SuccessTransition_AllRequiredStepsComplete_MarksSagaCompleted()
        {
            var transaction = new Mock<IOutboxTransaction>();
            var unitOfWork = new Mock<ISagaUnitOfWork>();
            unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = new Mock<ISagaRepository>();
            repository.Setup(x => x.FindRunningAsync("TestSaga", "order-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(saga);
            repository.Setup(x => x.FindStepAsync(7, "ReserveStock", It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaStep?)null);
            repository.Setup(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var completedStep = SagaStep.Create(7, "ReserveStock", "{}");
            completedStep.MarkCompleted();
            repository.Setup(x => x.GetStepsAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { completedStep });

            var guard = new Mock<IProcessedMessageGuard>();
            guard.Setup(x => x.TryMarkProcessedAsync(
                    10, It.IsAny<string>(), transaction.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "ReserveStock", SagaTransitionKind.Success, true),
                    null),
                unitOfWork,
                repository,
                new Mock<IOutboxWriter>(),
                guard,
                new TestPayloadSerializer());

            await handler.HandleAsync(new TestMessage("order-123"), 10);

            saga.Status.Should().Be(SagaInstanceStatus.Completed);
            repository.Verify(x => x.UpdateAsync(saga, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SuccessTransition_NotAllRequiredStepsComplete_LeavesSagaRunning()
        {
            var transaction = new Mock<IOutboxTransaction>();
            var unitOfWork = new Mock<ISagaUnitOfWork>();
            unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = new Mock<ISagaRepository>();
            repository.Setup(x => x.FindRunningAsync("TestSaga", "order-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(saga);
            repository.Setup(x => x.FindStepAsync(7, "ReserveStock", It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaStep?)null);
            repository.Setup(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            // Only "ReserveStock" is reported completed - "CreatePayment" (declared on the same
            // definition, triggered by a different message not fired in this test) is still pending.
            var completedStep = SagaStep.Create(7, "ReserveStock", "{}");
            completedStep.MarkCompleted();
            repository.Setup(x => x.GetStepsAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { completedStep });

            var guard = new Mock<IProcessedMessageGuard>();
            guard.Setup(x => x.TryMarkProcessedAsync(
                    10, It.IsAny<string>(), transaction.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new ISagaStepSpec[]
                    {
                        new TestStepSpec(typeof(TestMessage), "ReserveStock", SagaTransitionKind.Success, true),
                        new TestStepSpec(typeof(OtherTestMessage), "CreatePayment", SagaTransitionKind.Success, false),
                    },
                    null),
                unitOfWork,
                repository,
                new Mock<IOutboxWriter>(),
                guard,
                new TestPayloadSerializer());

            await handler.HandleAsync(new TestMessage("order-123"), 10);

            saga.Status.Should().Be(SagaInstanceStatus.Running);
            repository.Verify(x => x.UpdateAsync(It.IsAny<SagaInstance>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task NotifyTransition_EnqueuesNotifyMessage_LeavesSagaStatusUnchanged()
        {
            var transaction = new Mock<IOutboxTransaction>();
            var unitOfWork = new Mock<ISagaUnitOfWork>();
            unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = new Mock<ISagaRepository>();
            SagaStep? createdStep = null;
            repository.Setup(x => x.FindRunningAsync("TestSaga", "order-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(saga);
            repository.Setup(x => x.FindStepAsync(7, "NotifyCustomer", It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaStep?)null);
            repository.Setup(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()))
                .Callback<SagaStep, CancellationToken>((step, _) => createdStep = step)
                .Returns(Task.CompletedTask);

            var guard = new Mock<IProcessedMessageGuard>();
            guard.Setup(x => x.TryMarkProcessedAsync(
                    10, It.IsAny<string>(), transaction.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var outboxWriter = new Mock<IOutboxWriter>();
            var notification = new TestNotification("order-123");
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(
                        typeof(TestMessage),
                        "NotifyCustomer",
                        SagaTransitionKind.Notify,
                        false,
                        _ => notification),
                    null),
                unitOfWork,
                repository,
                outboxWriter,
                guard,
                new TestPayloadSerializer());

            await handler.HandleAsync(new TestMessage("order-123"), 10);

            saga.Status.Should().Be(SagaInstanceStatus.Running);
            createdStep!.Status.Should().Be(SagaStepStatus.Completed);
            outboxWriter.Verify(
                x => x.EnqueueAsync(notification, transaction.Object, It.IsAny<CancellationToken>()),
                Times.Once);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task NotifyTransition_ReadsExistingStepsBeforePersistingCurrentStep_NoDuplicateInContext()
        {
            // Regression test: SagaRepository.AddStepAsync calls SaveChangesAsync immediately, so
            // within the same transaction a GetStepsAsync call issued *after* AddStepAsync would see
            // the just-added step, and appending it again would give SagaTransitionContext two
            // SagaStepPayloads with the same StepName -- its ToDictionary(...) throws ArgumentException
            // on that duplicate key. This test mocks GetStepsAsync to only "see" a step once AddStepAsync
            // has actually run, so it fails if the handler ever goes back to querying before persisting.
            var transaction = new Mock<IOutboxTransaction>();
            var unitOfWork = new Mock<ISagaUnitOfWork>();
            unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = new Mock<ISagaRepository>();
            SagaStep? createdStep = null;
            repository.Setup(x => x.FindRunningAsync("TestSaga", "order-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(saga);
            repository.Setup(x => x.FindStepAsync(7, "NotifyCustomer", It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaStep?)null);
            repository.Setup(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()))
                .Callback<SagaStep, CancellationToken>((step, _) => createdStep = step)
                .Returns(Task.CompletedTask);
            repository.Setup(x => x.GetStepsAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => createdStep is null
                    ? Array.Empty<SagaStep>()
                    : new[] { createdStep });

            var guard = new Mock<IProcessedMessageGuard>();
            guard.Setup(x => x.TryMarkProcessedAsync(
                    10, It.IsAny<string>(), transaction.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var outboxWriter = new Mock<IOutboxWriter>();
            var notification = new TestNotification("order-123");
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(
                        typeof(TestMessage),
                        "NotifyCustomer",
                        SagaTransitionKind.Notify,
                        false,
                        _ => notification),
                    null),
                unitOfWork,
                repository,
                outboxWriter,
                guard,
                new TestPayloadSerializer());

            Func<Task> act = () => handler.HandleAsync(new TestMessage("order-123"), 10);

            await act.Should().NotThrowAsync();
            createdStep!.Status.Should().Be(SagaStepStatus.Completed);
            outboxWriter.Verify(
                x => x.EnqueueAsync(notification, transaction.Object, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifyTransition_WithoutNotifyFactory_RecordsStepButEnqueuesNothing()
        {
            var transaction = new Mock<IOutboxTransaction>();
            var unitOfWork = new Mock<ISagaUnitOfWork>();
            unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = new Mock<ISagaRepository>();
            SagaStep? createdStep = null;
            repository.Setup(x => x.FindRunningAsync("TestSaga", "order-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(saga);
            repository.Setup(x => x.FindStepAsync(7, "NotifyCustomer", It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaStep?)null);
            repository.Setup(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()))
                .Callback<SagaStep, CancellationToken>((step, _) => createdStep = step)
                .Returns(Task.CompletedTask);

            var guard = new Mock<IProcessedMessageGuard>();
            guard.Setup(x => x.TryMarkProcessedAsync(
                    10, It.IsAny<string>(), transaction.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var outboxWriter = new Mock<IOutboxWriter>();
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "NotifyCustomer", SagaTransitionKind.Notify, false),
                    null),
                unitOfWork,
                repository,
                outboxWriter,
                guard,
                new TestPayloadSerializer());

            await handler.HandleAsync(new TestMessage("order-123"), 10);

            saga.Status.Should().Be(SagaInstanceStatus.Running);
            createdStep!.Status.Should().Be(SagaStepStatus.Completed);
            outboxWriter.Verify(
                x => x.EnqueueAsync(
                    It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private static SagaTransitionHandler<TestMessage> CreateHandler(
            ISagaDefinition definition,
            Mock<ISagaUnitOfWork> unitOfWork,
            Mock<ISagaRepository> repository,
            Mock<IOutboxWriter> outboxWriter,
            Mock<IProcessedMessageGuard> guard,
            ISagaPayloadSerializer payloadSerializer)
        {
            return new SagaTransitionHandler<TestMessage>(
                new[] { definition },
                unitOfWork.Object,
                repository.Object,
                outboxWriter.Object,
                guard.Object,
                payloadSerializer);
        }

        private static void SetId(SagaInstance instance, long id)
        {
            typeof(SagaInstance).GetProperty(nameof(SagaInstance.Id))!.SetValue(instance, id);
        }

        private sealed record TestMessage(string CorrelationId) : IMessage;

        private sealed record OtherTestMessage(string CorrelationId) : IMessage;

        private sealed record TestCompensation(string CorrelationId) : IMessage;

        private sealed record TestNotification(string CorrelationId) : IMessage;

        private sealed class TestSagaDefinition : ISagaDefinition
        {
            public TestSagaDefinition(ISagaStepSpec step, Func<SagaTransitionContext, IMessage>? compensationFactory)
                : this(new[] { step }, compensationFactory)
            {
            }

            public TestSagaDefinition(IReadOnlyList<ISagaStepSpec> steps, Func<SagaTransitionContext, IMessage>? compensationFactory)
            {
                Steps = steps;
                CompensationFactory = compensationFactory;
            }

            public string SagaType => "TestSaga";
            public IReadOnlyList<ISagaStepSpec> Steps { get; }
            public Func<SagaTransitionContext, IMessage>? CompensationFactory { get; }
        }

        private sealed class TestStepSpec : ISagaStepSpec
        {
            public TestStepSpec(
                Type messageType,
                string stepName,
                SagaTransitionKind kind,
                bool startsNewInstance,
                Func<SagaTransitionContext, IMessage>? notifyFactory = null)
            {
                MessageType = messageType;
                StepName = stepName;
                Kind = kind;
                StartsNewInstance = startsNewInstance;
                NotifyFactory = notifyFactory;
            }

            public Type MessageType { get; }
            public string StepName { get; }
            public SagaTransitionKind Kind { get; }
            public bool StartsNewInstance { get; }
            public Func<IMessage, string> ExtractCorrelationId => message => ((TestMessage)message).CorrelationId;
            public Func<SagaTransitionContext, IMessage>? NotifyFactory { get; }
        }

        private sealed class TestPayloadSerializer : ISagaPayloadSerializer
        {
            public string Serialize(IMessage message)
            {
                return $"{{\"CorrelationId\":\"{((TestMessage)message).CorrelationId}\"}}";
            }

            public IMessage Deserialize(string payload, Type messageType)
            {
                var correlationId = payload.Split('"')[3];
                return new TestMessage(correlationId);
            }
        }
    }
}