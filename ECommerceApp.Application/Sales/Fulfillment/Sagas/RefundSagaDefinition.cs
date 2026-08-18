using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sagas;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ECommerceApp.Application.Sales.Fulfillment.Sagas
{
    public sealed class RefundSagaDefinition : ISagaDefinition
    {
        public string SagaType => "Refund";

        public IReadOnlyList<ISagaStepSpec> Steps { get; } = new[]
        {
            new SagaStepSpec(
                typeof(RefundApproved),
                "RefundApproved",
                SagaTransitionKind.Success,
                true,
                message => ((RefundApproved)message).RefundId.ToString(CultureInfo.InvariantCulture)),
            new SagaStepSpec(
                typeof(RefundStockReturned),
                "RefundStockReturned",
                SagaTransitionKind.Success,
                false,
                message => ((RefundStockReturned)message).RefundId.ToString(CultureInfo.InvariantCulture)),
            new SagaStepSpec(
                typeof(RefundCustomerNotified),
                "RefundCustomerNotified",
                SagaTransitionKind.Success,
                false,
                message => ((RefundCustomerNotified)message).RefundId.ToString(CultureInfo.InvariantCulture))
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