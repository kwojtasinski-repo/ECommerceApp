using AwesomeAssertions;
using ECommerceApp.Application.Inventory.Availability.Messages;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Application.Presale.Checkout.Sagas;
using ECommerceApp.Application.Sagas;
using ECommerceApp.Infrastructure.Sagas;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Sagas
{
    public class AvailabilityReservationChangeSagaDefinitionTests
    {
        [Fact]
        public void Definition_ContainsExpectedNotifyStepAndFactory()
        {
            var definition = new AvailabilityReservationChangeSagaDefinition();
            var message = new StockAvailabilityChanged(42, 3, DateTime.UtcNow);

            definition.SagaType.Should().Be("AvailabilityReservationChange");
            definition.CompensationFactory.Should().BeNull();
            definition.Steps.Should().HaveCount(1);
            definition.Steps[0].MessageType.Should().Be(typeof(StockAvailabilityChanged));
            definition.Steps[0].StepName.Should().Be("StockAvailabilityChanged");
            definition.Steps[0].Kind.Should().Be(SagaTransitionKind.Notify);
            definition.Steps[0].StartsNewInstance.Should().BeTrue();
            definition.Steps[0].ExtractCorrelationId(message).Should().Be("42");

            var saga = Domain.Sagas.SagaInstance.Create(definition.SagaType, "42");
            var context = new SagaTransitionContext(
                saga,
                new[] { new SagaStepPayload(
                    "StockAvailabilityChanged",
                    typeof(StockAvailabilityChanged),
                    new TestPayloadSerializer().Serialize(message)) },
                new TestPayloadSerializer());
            var notification = definition.Steps[0].NotifyFactory!(context)
                .Should().BeOfType<CheckoutReservationAvailabilityDropped>().Subject;

            notification.ProductId.Should().Be(42);
            notification.AvailableQuantity.Should().Be(3);
            notification.OccurredAt.Should().Be(message.OccurredAt);
        }

        private sealed class TestPayloadSerializer : ISagaPayloadSerializer
        {
            public string Serialize(ECommerceApp.Application.Messaging.IMessage message)
                => $"{((StockAvailabilityChanged)message).ProductId}|{((StockAvailabilityChanged)message).AvailableQuantity}|{((StockAvailabilityChanged)message).OccurredAt:O}";

            public ECommerceApp.Application.Messaging.IMessage Deserialize(string payload, Type messageType)
            {
                var parts = payload.Split('|');
                return new StockAvailabilityChanged(
                    int.Parse(parts[0]),
                    int.Parse(parts[1]),
                    DateTime.Parse(parts[2], null, System.Globalization.DateTimeStyles.RoundtripKind));
            }
        }
    }
}