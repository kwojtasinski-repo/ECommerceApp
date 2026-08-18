using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Application.Sagas;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Presale.Checkout.Sagas
{
    public sealed class CartRecoverySagaDefinition : ISagaDefinition
    {
        public string SagaType => "CartRecovery";

        public IReadOnlyList<ISagaStepSpec> Steps { get; } = new[]
        {
            new SagaStepSpec(
                typeof(CheckoutReservationRevertRequested),
                "CheckoutReservationRevertRequested",
                SagaTransitionKind.Success,
                true,
                message => ((CheckoutReservationRevertRequested)message).UserId)
        };

        public Func<SagaTransitionContext, IMessage>? CompensationFactory => null;

        private sealed class SagaStepSpec : ISagaStepSpec
        {
            public SagaStepSpec(
                Type messageType,
                string stepName,
                SagaTransitionKind kind,
                bool startsNewInstance,
                Func<IMessage, string> extractCorrelationId)
            {
                MessageType = messageType;
                StepName = stepName;
                Kind = kind;
                StartsNewInstance = startsNewInstance;
                ExtractCorrelationId = extractCorrelationId;
            }

            public Type MessageType { get; }
            public string StepName { get; }
            public SagaTransitionKind Kind { get; }
            public bool StartsNewInstance { get; }
            public Func<IMessage, string> ExtractCorrelationId { get; }
            public Func<SagaTransitionContext, IMessage>? NotifyFactory => null;
        }
    }
}