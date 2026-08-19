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
        private readonly Mock<IOutboxTransaction> _transaction = new();
        private readonly Mock<ISagaUnitOfWork> _unitOfWork = new();
        private readonly Mock<ISagaRepository> _repository = new();
        private readonly Mock<IProcessedMessageGuard> _guard = new();
        private readonly Mock<IOutboxWriter> _outboxWriter = new();

        [Fact]
        public async Task SuccessTransition_CreatesSagaAndCompletesStep()
        {
            // Arrange
            SagaInstance? createdInstance = null;
            SagaStep? createdStep = null;
            SetupNoRunningSaga(_repository, "TestSaga", "order-123");
            SetupSagaAdded(_repository, 7, instance => createdInstance = instance);
            SetupMissingStep(_repository, 7, "ReserveStock");
            SetupStepAdded(_repository, step => createdStep = step);
            SetupStepsAvailableAfterStepAdded(_repository, 7, () => createdStep);
            SetupAcceptedGuard();
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "ReserveStock", SagaTransitionKind.Success, true),
                    null));

            // Act
            await handler.HandleAsync(new TestMessage("order-123"), 10);

            // Assert
            createdInstance.Should().NotBeNull();
            createdStep.Should().NotBeNull();
            createdStep!.Status.Should().Be(SagaStepStatus.Completed);
            _transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Redelivery_WhenGuardRejects_IsNoOp()
        {
            // Arrange
            SetupRejectedGuard();
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "ReserveStock", SagaTransitionKind.Success, true),
                    null));

            // Act
            await handler.HandleAsync(new TestMessage("order-123"), 10);

            // Assert
            _repository.Verify(x => x.FindRunningAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task FailureTransition_MarksSagaCompensatingAndEnqueuesCompensation()
        {
            // Arrange
            var transaction = _transaction;
            var unitOfWork = _unitOfWork;
            SetupTransaction(unitOfWork, transaction);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = _repository;
            SagaStep? createdStep = null;
            SetupRunningSaga(repository, "TestSaga", "order-123", saga);
            SetupMissingStep(repository, 7, "PaymentFailed");
            SetupStepAdded(repository, step => createdStep = step);
            SetupStepsAvailableAfterStepAdded(repository, 7, () => createdStep);

            var guard = _guard;
            SetupGuard(guard, transaction, 10, true);
            var outboxWriter = _outboxWriter;
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

            // Act
            await handler.HandleAsync(new TestMessage("order-123"), 10);

            // Assert
            saga.Status.Should().Be(SagaInstanceStatus.Compensating);
            createdStep!.Status.Should().Be(SagaStepStatus.Failed);
            outboxWriter.Verify(x => x.EnqueueAsync(compensation, transaction.Object, It.IsAny<CancellationToken>()), Times.Once);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SuccessTransition_StepDoesNotStartNewInstance_WhenNoRunningSaga_IsSkipped()
        {
            // Arrange
            var transaction = _transaction;
            var unitOfWork = _unitOfWork;
            SetupTransaction(unitOfWork, transaction);
            var repository = _repository;
            SetupNoRunningSaga(repository, "TestSaga", "order-123");
            var guard = _guard;
            SetupGuard(guard, transaction, 10, true);
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "ReturnStock", SagaTransitionKind.Success, false),
                    null),
                unitOfWork,
                repository,
                _outboxWriter,
                guard,
                new TestPayloadSerializer());

            // Act
            await handler.HandleAsync(new TestMessage("order-123"), 10);

            // A non-starting step arriving with no running instance (e.g. out-of-order delivery)
            // must not spuriously create one - regardless of whether the step is Success or Failure
            // kind. See SagaTransitionHandler.cs's saga-is-null branch.
            // Assert
            repository.Verify(x => x.AddAsync(It.IsAny<SagaInstance>(), It.IsAny<CancellationToken>()), Times.Never);
            repository.Verify(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SuccessTransition_AllRequiredStepsComplete_MarksSagaCompleted()
        {
            // Arrange
            var transaction = _transaction;
            var unitOfWork = _unitOfWork;
            SetupTransaction(unitOfWork, transaction);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = _repository;
            SetupRunningSaga(repository, "TestSaga", "order-123", saga);
            SetupMissingStep(repository, 7, "ReserveStock");
            SetupStepAdded(repository, _ => { });
            var completedStep = SagaStep.Create(7, "ReserveStock", "{}");
            completedStep.MarkCompleted();
            SetupStepsAvailable(repository, 7, () => new[] { completedStep });

            var guard = _guard;
            SetupGuard(guard, transaction, 10, true);
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "ReserveStock", SagaTransitionKind.Success, true),
                    null),
                unitOfWork,
                repository,
                _outboxWriter,
                guard,
                new TestPayloadSerializer());

            // Act
            await handler.HandleAsync(new TestMessage("order-123"), 10);

            // Assert
            saga.Status.Should().Be(SagaInstanceStatus.Completed);
            repository.Verify(x => x.UpdateAsync(saga, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SuccessTransition_NotAllRequiredStepsComplete_LeavesSagaRunning()
        {
            // Arrange
            var transaction = _transaction;
            var unitOfWork = _unitOfWork;
            SetupTransaction(unitOfWork, transaction);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = _repository;
            SetupRunningSaga(repository, "TestSaga", "order-123", saga);
            SetupMissingStep(repository, 7, "ReserveStock");
            SetupStepAdded(repository, _ => { });
            // Only "ReserveStock" is reported completed - "CreatePayment" (declared on the same
            // definition, triggered by a different message not fired in this test) is still pending.
            var completedStep = SagaStep.Create(7, "ReserveStock", "{}");
            completedStep.MarkCompleted();
            SetupStepsAvailable(repository, 7, () => new[] { completedStep });

            var guard = _guard;
            SetupGuard(guard, transaction, 10, true);
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
                _outboxWriter,
                guard,
                new TestPayloadSerializer());

            // Act
            await handler.HandleAsync(new TestMessage("order-123"), 10);

            // Assert
            saga.Status.Should().Be(SagaInstanceStatus.Running);
            repository.Verify(x => x.UpdateAsync(It.IsAny<SagaInstance>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task NotifyTransition_EnqueuesNotifyMessage_CompletesSaga()
        {
            // Arrange
            var transaction = _transaction;
            var unitOfWork = _unitOfWork;
            SetupTransaction(unitOfWork, transaction);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = _repository;
            SagaStep? createdStep = null;
            SetupRunningSaga(repository, "TestSaga", "order-123", saga);
            SetupMissingStep(repository, 7, "NotifyCustomer");
            SetupStepAdded(repository, step => createdStep = step);
            SetupStepsAvailableAfterStepAdded(repository, 7, () => createdStep);

            var guard = _guard;
            SetupGuard(guard, transaction, 10, true);
            var outboxWriter = _outboxWriter;
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

            // Act
            await handler.HandleAsync(new TestMessage("order-123"), 10);

            // Assert
            saga.Status.Should().Be(SagaInstanceStatus.Completed);
            createdStep!.Status.Should().Be(SagaStepStatus.Completed);
            outboxWriter.Verify(
                x => x.EnqueueAsync(notification, transaction.Object, It.IsAny<CancellationToken>()),
                Times.Once);
            transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task NotifyTransition_SecondDeliveryAfterCompletion_CreatesNewInstanceAndNotifiesAgain()
        {
            // Arrange
            var transaction = _transaction;
            var unitOfWork = _unitOfWork;
            SetupTransaction(unitOfWork, transaction);

            var firstSaga = SagaInstance.Create("TestSaga", "order-123");
            SetId(firstSaga, 7);
            SagaInstance? secondSaga = null;
            var repository = _repository;
            var findRunningCalls = 0;
            var addedSteps = new Dictionary<long, List<SagaStep>>();
            SetupRedeliveryRepository(
                repository,
                firstSaga,
                () => ++findRunningCalls,
                instance => secondSaga = instance,
                addedSteps);

            var guard = _guard;
            SetupGuard(guard, transaction, 10, true);
            var outboxWriter = _outboxWriter;
            var notification = new TestNotification("order-123");
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(
                        typeof(TestMessage),
                        "NotifyCustomer",
                        SagaTransitionKind.Notify,
                        true,
                        _ => notification),
                    null),
                unitOfWork,
                repository,
                outboxWriter,
                guard,
                new TestPayloadSerializer());

            // Act
            await handler.HandleAsync(new TestMessage("order-123"), 10);
            await handler.HandleAsync(new TestMessage("order-123"), 10);

            // Assert
            firstSaga.Status.Should().Be(SagaInstanceStatus.Completed);
            secondSaga.Should().NotBeNull();
            secondSaga!.Status.Should().Be(SagaInstanceStatus.Completed);
            repository.Verify(x => x.AddAsync(It.IsAny<SagaInstance>(), It.IsAny<CancellationToken>()), Times.Once);
            outboxWriter.Verify(
                x => x.EnqueueAsync(notification, transaction.Object, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task NotifyTransition_ReadsExistingStepsBeforePersistingCurrentStep_NoDuplicateInContext()
        {
            // Arrange
            // Regression test: SagaRepository.AddStepAsync calls SaveChangesAsync immediately, so
            // within the same transaction a GetStepsAsync call issued *after* AddStepAsync would see
            // the just-added step, and appending it again would give SagaTransitionContext two
            // SagaStepPayloads with the same StepName -- its ToDictionary(...) throws ArgumentException
            // on that duplicate key. This test mocks GetStepsAsync to only "see" a step once AddStepAsync
            // has actually run, so it fails if the handler ever goes back to querying before persisting.
            var transaction = _transaction;
            var unitOfWork = _unitOfWork;
            SetupTransaction(unitOfWork, transaction);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = _repository;
            SagaStep? createdStep = null;
            SetupRunningSaga(repository, "TestSaga", "order-123", saga);
            SetupMissingStep(repository, 7, "NotifyCustomer");
            SetupStepAdded(repository, step => createdStep = step);
            SetupStepsAvailableAfterStepAdded(repository, 7, () => createdStep);

            var guard = _guard;
            SetupGuard(guard, transaction, 10, true);
            var outboxWriter = _outboxWriter;
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

            // Act
            Func<Task> act = () => handler.HandleAsync(new TestMessage("order-123"), 10);

            // Assert
            await act.Should().NotThrowAsync();
            createdStep!.Status.Should().Be(SagaStepStatus.Completed);
            outboxWriter.Verify(
                x => x.EnqueueAsync(notification, transaction.Object, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifyTransition_WithoutNotifyFactory_RecordsStepButEnqueuesNothing()
        {
            // Arrange
            var transaction = _transaction;
            var unitOfWork = _unitOfWork;
            SetupTransaction(unitOfWork, transaction);

            var saga = SagaInstance.Create("TestSaga", "order-123");
            SetId(saga, 7);
            var repository = _repository;
            SagaStep? createdStep = null;
            SetupRunningSaga(repository, "TestSaga", "order-123", saga);
            SetupMissingStep(repository, 7, "NotifyCustomer");
            SetupStepAdded(repository, step => createdStep = step);

            var guard = _guard;
            SetupGuard(guard, transaction, 10, true);
            var outboxWriter = _outboxWriter;
            var handler = CreateHandler(
                new TestSagaDefinition(
                    new TestStepSpec(typeof(TestMessage), "NotifyCustomer", SagaTransitionKind.Notify, false),
                    null),
                unitOfWork,
                repository,
                outboxWriter,
                guard,
                new TestPayloadSerializer());

            // Act
            await handler.HandleAsync(new TestMessage("order-123"), 10);

            // Assert
            saga.Status.Should().Be(SagaInstanceStatus.Running);
            createdStep!.Status.Should().Be(SagaStepStatus.Completed);
            outboxWriter.Verify(
                x => x.EnqueueAsync(
                    It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private static SagaTransitionHandler<TestMessage> CreateHandler(
            ISagaDefinition definition,
            ISagaPayloadSerializer payloadSerializer,
            Mock<ISagaUnitOfWork> unitOfWork,
            Mock<ISagaRepository> repository,
            Mock<IOutboxWriter> outboxWriter,
            Mock<IProcessedMessageGuard> guard)
        {
            return new SagaTransitionHandler<TestMessage>(
                new[] { definition },
                unitOfWork.Object,
                repository.Object,
                outboxWriter.Object,
                guard.Object,
                payloadSerializer);
        }

        private static SagaTransitionHandler<TestMessage> CreateHandler(
            ISagaDefinition definition,
            Mock<ISagaUnitOfWork> unitOfWork,
            Mock<ISagaRepository> repository,
            Mock<IOutboxWriter> outboxWriter,
            Mock<IProcessedMessageGuard> guard,
            ISagaPayloadSerializer payloadSerializer)
        {
            return CreateHandler(
                definition,
                payloadSerializer,
                unitOfWork,
                repository,
                outboxWriter,
                guard);
        }

        private SagaTransitionHandler<TestMessage> CreateHandler(
            ISagaDefinition definition,
            ISagaPayloadSerializer? payloadSerializer = null)
        {
            SetupTransaction(_unitOfWork, _transaction);
            return new SagaTransitionHandler<TestMessage>(
                new[] { definition },
                _unitOfWork.Object,
                _repository.Object,
                _outboxWriter.Object,
                _guard.Object,
                payloadSerializer ?? new TestPayloadSerializer());
        }

        private static void SetupTransaction(
            Mock<ISagaUnitOfWork> unitOfWork,
            Mock<IOutboxTransaction> transaction)
        {
            unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);
        }

        private static void SetupGuard(
            Mock<IProcessedMessageGuard> guard,
            Mock<IOutboxTransaction> transaction,
            long outboxMessageId,
            bool accepted)
        {
            guard.Setup(x => x.TryMarkProcessedAsync(
                    outboxMessageId,
                    It.IsAny<string>(),
                    transaction.Object,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(accepted);
        }

        private void SetupAcceptedGuard()
        {
            SetupGuard(_guard, _transaction, 10, true);
        }

        private void SetupRejectedGuard()
        {
            SetupGuard(_guard, _transaction, 10, false);
        }

        private static void SetupNoRunningSaga(
            Mock<ISagaRepository> repository,
            string sagaType,
            string correlationId)
        {
            repository.Setup(x => x.FindRunningAsync(sagaType, correlationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaInstance?)null);
        }

        private static void SetupRunningSaga(
            Mock<ISagaRepository> repository,
            string sagaType,
            string correlationId,
            SagaInstance saga)
        {
            repository.Setup(x => x.FindRunningAsync(sagaType, correlationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(saga);
        }

        private static void SetupMissingStep(
            Mock<ISagaRepository> repository,
            long sagaId,
            string stepName)
        {
            repository.Setup(x => x.FindStepAsync(sagaId, stepName, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaStep?)null);
        }

        private static void SetupSagaAdded(
            Mock<ISagaRepository> repository,
            long sagaId,
            Action<SagaInstance> onAdded)
        {
            repository.Setup(x => x.AddAsync(It.IsAny<SagaInstance>(), It.IsAny<CancellationToken>()))
                .Callback<SagaInstance, CancellationToken>((instance, _) =>
                {
                    SetId(instance, sagaId);
                    onAdded(instance);
                })
                .Returns(Task.CompletedTask);
        }

        private static void SetupStepAdded(
            Mock<ISagaRepository> repository,
            Action<SagaStep> onAdded)
        {
            repository.Setup(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()))
                .Callback<SagaStep, CancellationToken>((step, _) => onAdded(step))
                .Returns(Task.CompletedTask);
        }

        private static void SetupStepsAvailable(
            Mock<ISagaRepository> repository,
            long sagaId,
            Func<IReadOnlyList<SagaStep>> steps)
        {
            repository.Setup(x => x.GetStepsAsync(sagaId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(steps);
        }

        private static void SetupStepsAvailable(
            Mock<ISagaRepository> repository,
            long sagaId,
            IReadOnlyList<SagaStep> steps)
        {
            SetupStepsAvailable(repository, sagaId, () => steps);
        }

        private static void SetupStepsAvailableAfterStepAdded(
            Mock<ISagaRepository> repository,
            long sagaId,
            Func<SagaStep?> getStep)
        {
            SetupStepsAvailable(
                repository,
                sagaId,
                () => getStep() is { } step
                    ? new[] { step }
                    : Array.Empty<SagaStep>());
        }

        private static void SetupRedeliveryRepository(
            Mock<ISagaRepository> repository,
            SagaInstance firstSaga,
            Func<int> nextFindRunningCall,
            Action<SagaInstance> onSecondSagaAdded,
            Dictionary<long, List<SagaStep>> addedSteps)
        {
            repository.Setup(x => x.FindRunningAsync("TestSaga", "order-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => nextFindRunningCall() == 1 ? firstSaga : null);
            repository.Setup(x => x.AddAsync(It.IsAny<SagaInstance>(), It.IsAny<CancellationToken>()))
                .Callback<SagaInstance, CancellationToken>((instance, _) =>
                {
                    SetId(instance, 8);
                    onSecondSagaAdded(instance);
                })
                .Returns(Task.CompletedTask);
            SetupMissingStepForAnySaga(repository, "NotifyCustomer");
            SetupStepsTrackedBySaga(repository, addedSteps);
        }

        private static void SetupMissingStepForAnySaga(
            Mock<ISagaRepository> repository,
            string stepName)
        {
            repository.Setup(x => x.FindStepAsync(It.IsAny<long>(), stepName, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SagaStep?)null);
        }

        private static void SetupStepsTrackedBySaga(
            Mock<ISagaRepository> repository,
            Dictionary<long, List<SagaStep>> addedSteps)
        {
            repository.Setup(x => x.AddStepAsync(It.IsAny<SagaStep>(), It.IsAny<CancellationToken>()))
                .Callback<SagaStep, CancellationToken>((step, _) =>
                {
                    if (!addedSteps.TryGetValue(step.SagaInstanceId, out var steps))
                    {
                        steps = new List<SagaStep>();
                        addedSteps[step.SagaInstanceId] = steps;
                    }

                    steps.Add(step);
                })
                .Returns(Task.CompletedTask);
            repository.Setup(x => x.GetStepsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((long sagaId, CancellationToken _) =>
                    addedSteps.TryGetValue(sagaId, out var steps)
                        ? steps
                        : Array.Empty<SagaStep>());
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