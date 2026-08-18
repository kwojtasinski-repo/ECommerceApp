using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Application.Sagas;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ECommerceApp.Application.Presale.Checkout.Sagas
{
    public sealed class AvailabilityReservationChangeSagaDefinition : ISagaDefinition
    {
        public string SagaType => "AvailabilityReservationChange";

        public IReadOnlyList<ISagaStepSpec> Steps { get; } = new[]
        {
            new SagaStepSpec()
        };

        public Func<SagaTransitionContext, IMessage>? CompensationFactory => null;

        private sealed class SagaStepSpec : ISagaStepSpec
        {
            public Type MessageType => typeof(StockAvailabilityChanged);
            public string StepName => "StockAvailabilityChanged";
            public SagaTransitionKind Kind => SagaTransitionKind.Notify;
            public bool StartsNewInstance => true;
            public Func<IMessage, string> ExtractCorrelationId => message =>
                ((StockAvailabilityChanged)message).ProductId.ToString(CultureInfo.InvariantCulture);
            public Func<SagaTransitionContext, IMessage>? NotifyFactory => context =>
            {
                var message = context.Get<StockAvailabilityChanged>("StockAvailabilityChanged");
                return new CheckoutReservationAvailabilityDropped(
                    message.ProductId,
                    message.AvailableQuantity,
                    message.OccurredAt);
            };
        }
    }
}