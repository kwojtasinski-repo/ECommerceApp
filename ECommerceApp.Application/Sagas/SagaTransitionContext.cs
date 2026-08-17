using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Sagas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Application.Sagas
{
    public sealed class SagaTransitionContext
    {
        private readonly IReadOnlyDictionary<string, SagaStepPayload> _steps;
        private readonly ISagaPayloadSerializer _serializer;

        public SagaTransitionContext(
            SagaInstance instance,
            IEnumerable<SagaStepPayload> steps,
            ISagaPayloadSerializer serializer)
        {
            Instance = instance;
            _steps = steps.ToDictionary(step => step.StepName, StringComparer.Ordinal);
            _serializer = serializer;
        }

        public SagaInstance Instance { get; }

        public TMessage Get<TMessage>(string stepName)
            where TMessage : class, IMessage
        {
            if (!_steps.TryGetValue(stepName, out var step))
            {
                throw new InvalidOperationException(
                    $"Saga step '{stepName}' was not found in saga '{Instance.SagaType}'.");
            }

            if (step.MessageType != typeof(TMessage))
            {
                throw new InvalidOperationException(
                    $"Saga step '{stepName}' contains '{step.MessageType.FullName}', " +
                    $"not '{typeof(TMessage).FullName}'.");
            }

            return (TMessage)_serializer.Deserialize(step.Payload, typeof(TMessage));
        }
    }
}