using AwesomeAssertions;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Presale.Checkout;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class CheckoutReservationRevertRequestedHandlerTests
    {
        [Fact]
        public async Task HandleAsync_RevertsReservationsForMessageUser()
        {
            var service = new Mock<ISoftReservationService>();
            var handler = new CheckoutReservationRevertRequestedHandler(service.Object);
            var userId = new PresaleUserId("user-1");

            await handler.HandleAsync(new CheckoutReservationRevertRequested(userId.Value), CancellationToken.None);

            service.Verify(s => s.RevertAllForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}