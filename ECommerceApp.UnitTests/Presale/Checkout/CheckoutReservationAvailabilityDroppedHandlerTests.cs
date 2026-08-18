using AwesomeAssertions;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Application.Presale.Checkout.Services;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class CheckoutReservationAvailabilityDroppedHandlerTests
    {
        [Fact]
        public async Task HandleAsync_InvalidatesExcessForMessageProduct()
        {
            var service = new Mock<ISoftReservationService>();
            var handler = new CheckoutReservationAvailabilityDroppedHandler(service.Object);
            var message = new CheckoutReservationAvailabilityDropped(42, 3, DateTime.UtcNow);

            await handler.HandleAsync(message, CancellationToken.None);

            service.Verify(s => s.InvalidateExcessForProductAsync(42, 3, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}