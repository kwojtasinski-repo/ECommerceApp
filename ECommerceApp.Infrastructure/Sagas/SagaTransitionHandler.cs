using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sagas;
using ECommerceApp.Domain.Sagas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sagas
{
    internal sealed class SagaTransitionHandler<TMessage> : IIdAwareMessageHandler<TMessage>
        where TMessage : class, IMessage
    {
        private readonly IEnumerable<ISagaDefinition> _definitions;
        private readonly ISagaUnitOfWork _unitOfWork;
        private readonly ISagaRepository _repository;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IProcessedMessageGuard _processedMessageGuard;
        private readonly ISagaPayloadSerializer _payloadSerializer;

        public SagaTransitionHandler(
            IEnumerable<ISagaDefinition> definitions,
            ISagaUnitOfWork unitOfWork,
            ISagaRepository repository,
            IOutboxWriter outboxWriter,
            IProcessedMessageGuard processedMessageGuard,
            ISagaPayloadSerializer payloadSerializer)
        {
            _definitions = definitions;
            _unitOfWork = unitOfWork;
            _repository = repository;
            _outboxWriter = outboxWriter;
            _processedMessageGuard = processedMessageGuard;
            _payloadSerializer = payloadSerializer;
        }

        public Task HandleAsync(TMessage message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(TMessage message, long outboxMessageId, CancellationToken ct = default)
        {
            var transaction = await _unitOfWork.BeginTransactionAsync(ct);
            await using (transaction)
            {
                var handlerType = GetType().FullName
                    ?? throw new InvalidOperationException("Handler type name is unavailable.");
                if (!await _processedMessageGuard.TryMarkProcessedAsync(
                        outboxMessageId, handlerType, transaction, ct))
                {
                    return;
                }

                var payload = _payloadSerializer.Serialize(message);
                foreach (var definition in _definitions)
                {
                    foreach (var stepSpec in definition.Steps.Where(step => step.MessageType == typeof(TMessage)))
                    {
                        var correlationId = stepSpec.ExtractCorrelationId(message);
                        var saga = await _repository.FindRunningAsync(definition.SagaType, correlationId, ct);
                        if (saga is null)
                        {
                            if (!stepSpec.StartsNewInstance)
                            {
                                continue;
                            }

                            saga = SagaInstance.Create(definition.SagaType, correlationId);
                            await _repository.AddAsync(saga, ct);
                        }

                        var step = await _repository.FindStepAsync(saga.Id, stepSpec.StepName, ct);
                        if (step is not null)
                            continue;

                        step = SagaStep.Create(saga.Id, stepSpec.StepName, payload);
                        if (stepSpec.Kind == SagaTransitionKind.Success)
                        {
                            step.MarkCompleted();
                            await _repository.AddStepAsync(step, ct);

                            var requiredStepNames = definition.Steps
                                .Where(s => s.Kind == SagaTransitionKind.Success)
                                .Select(s => s.StepName)
                                .ToHashSet(StringComparer.Ordinal);
                            var completedStepNames = (await _repository.GetStepsAsync(saga.Id, ct)
                                    ?? Array.Empty<SagaStep>())
                                .Where(s => s.Status == SagaStepStatus.Completed)
                                .Select(s => s.StepName)
                                .ToHashSet(StringComparer.Ordinal);

                            if (requiredStepNames.IsSubsetOf(completedStepNames))
                            {
                                saga.MarkCompleted();
                                await _repository.UpdateAsync(saga, ct);
                            }

                            continue;
                        }

                        if (stepSpec.Kind == SagaTransitionKind.Notify)
                        {
                            step.MarkCompleted();

                            var notifySteps = await _repository.GetStepsAsync(saga.Id, ct)
                                ?? Array.Empty<SagaStep>();
                            var notifyContext = new SagaTransitionContext(
                                saga,
                                notifySteps.Select(existingStep => new SagaStepPayload(
                                        existingStep.StepName,
                                        ResolveMessageType(definition, existingStep.StepName),
                                        existingStep.Payload))
                                    .Append(new SagaStepPayload(
                                        step.StepName,
                                        stepSpec.MessageType,
                                        step.Payload)),
                                _payloadSerializer);

                            if (stepSpec.NotifyFactory is not null)
                            {
                                await _outboxWriter.EnqueueAsync(
                                    stepSpec.NotifyFactory(notifyContext), transaction, ct);
                            }

                            await _repository.AddStepAsync(step, ct);

                            continue;
                        }

                        step.MarkFailed();
                        var steps = await _repository.GetStepsAsync(saga.Id, ct)
                            ?? Array.Empty<SagaStep>();
                        var transitionContext = new SagaTransitionContext(
                            saga,
                            steps.Select(existingStep => new SagaStepPayload(
                                existingStep.StepName,
                                ResolveMessageType(definition, existingStep.StepName),
                                existingStep.Payload))
                                .Append(new SagaStepPayload(
                                    step.StepName,
                                    stepSpec.MessageType,
                                    step.Payload)),
                            _payloadSerializer);

                        if (definition.CompensationFactory is null)
                        {
                            saga.MarkFailed();
                        }
                        else
                        {
                            saga.MarkCompensating();
                            await _outboxWriter.EnqueueAsync(
                                definition.CompensationFactory(transitionContext), transaction, ct);
                        }

                        await _repository.UpdateAsync(saga, ct);

                        await _repository.AddStepAsync(step, ct);
                    }
                }

                await transaction.CommitAsync(ct);
            }
        }

        private static Type ResolveMessageType(ISagaDefinition definition, string stepName)
        {
            var step = definition.Steps.FirstOrDefault(candidate => candidate.StepName == stepName);
            if (step is null)
            {
                throw new InvalidOperationException(
                    $"Saga definition '{definition.SagaType}' has no step named '{stepName}'.");
            }

            return step.MessageType;
        }
    }
}