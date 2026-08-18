using AwesomeAssertions;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Application.Presale.Checkout.Sagas;
using ECommerceApp.Application.Sagas;
using Xunit;

namespace ECommerceApp.UnitTests.Sagas
{
    public class CartRecoverySagaDefinitionTests
    {
        [Fact]
        public void Definition_ContainsExpectedStepAndCorrelation()
        {
            var definition = new CartRecoverySagaDefinition();

            definition.SagaType.Should().Be("CartRecovery");
            definition.CompensationFactory.Should().BeNull();
            definition.Steps.Should().HaveCount(1);
            definition.Steps[0].MessageType.Should().Be(typeof(CheckoutReservationRevertRequested));
            definition.Steps[0].StepName.Should().Be("CheckoutReservationRevertRequested");
            definition.Steps[0].Kind.Should().Be(SagaTransitionKind.Success);
            definition.Steps[0].StartsNewInstance.Should().BeTrue();
            definition.Steps[0].ExtractCorrelationId(new CheckoutReservationRevertRequested("user-1"))
                .Should().Be("user-1");
        }
    }
}