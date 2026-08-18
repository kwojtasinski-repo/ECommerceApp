using AwesomeAssertions;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sagas;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Fulfillment.Sagas;
using System.Collections.Generic;
using Xunit;

namespace ECommerceApp.UnitTests.Sagas
{
    public class RefundSagaDefinitionTests
    {
        [Fact]
        public void Definition_ContainsExpectedSuccessStepsAndCorrelation()
        {
            var definition = new RefundSagaDefinition();

            definition.SagaType.Should().Be("Refund");
            definition.CompensationFactory.Should().BeNull();
            definition.Steps.Should().HaveCount(3);

            definition.Steps[0].MessageType.Should().Be(typeof(RefundApproved));
            definition.Steps[0].StepName.Should().Be("RefundApproved");
            definition.Steps[0].Kind.Should().Be(SagaTransitionKind.Success);
            definition.Steps[0].StartsNewInstance.Should().BeTrue();
            definition.Steps[0].ExtractCorrelationId(new RefundApproved(42, 7, new List<RefundApprovedItem>(), System.DateTime.UtcNow))
                .Should().Be("42");

            definition.Steps[1].MessageType.Should().Be(typeof(RefundStockReturned));
            definition.Steps[1].StepName.Should().Be("RefundStockReturned");
            definition.Steps[1].Kind.Should().Be(SagaTransitionKind.Success);
            definition.Steps[1].StartsNewInstance.Should().BeFalse();
            definition.Steps[1].ExtractCorrelationId(new RefundStockReturned(42)).Should().Be("42");

            definition.Steps[2].MessageType.Should().Be(typeof(RefundCustomerNotified));
            definition.Steps[2].StepName.Should().Be("RefundCustomerNotified");
            definition.Steps[2].Kind.Should().Be(SagaTransitionKind.Success);
            definition.Steps[2].StartsNewInstance.Should().BeFalse();
            definition.Steps[2].ExtractCorrelationId(new RefundCustomerNotified(42)).Should().Be("42");
        }
    }
}