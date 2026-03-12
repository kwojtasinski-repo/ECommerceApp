using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Presale.Checkout;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class OrderPlacedHandlerTests
    {
        private readonly Mock<ICartService> _cartService;
        private readonly Mock<ISoftReservationService> _softReservationService;
        private readonly OrderPlacedHandler _handler;

        public OrderPlacedHandlerTests()
        {
            _cartService = new Mock<ICartService>();
            _softReservationService = new Mock<ISoftReservationService>();
            _handler = new OrderPlacedHandler(_cartService.Object, _softReservationService.Object);
        }

        [Fact]
        public async Task HandleAsync_OrderPlaced_ShouldClearCartForUser()
        {
            var message = CreateMessage("user-1");

            await _handler.HandleAsync(message);

            _cartService.Verify(s => s.ClearAsync(
                It.Is<PresaleUserId>(id => id.Value == "user-1"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_OrderPlaced_ShouldRemoveAllSoftReservationsForUser()
        {
            var message = CreateMessage("user-1");

            await _handler.HandleAsync(message);

            _softReservationService.Verify(s => s.RemoveAllForUserAsync(
                "user-1",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_OrderPlaced_ShouldClearCartBeforeRemovingReservations()
        {
            var callOrder = new List<string>();
            _cartService.Setup(s => s.ClearAsync(It.IsAny<PresaleUserId>(), It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("cart"))
                .Returns(Task.CompletedTask);
            _softReservationService.Setup(s => s.RemoveAllForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("reservation"))
                .Returns(Task.CompletedTask);

            var message = CreateMessage("user-1");

            await _handler.HandleAsync(message);

            Assert.Equal(new[] { "cart", "reservation" }, callOrder);
        }

        [Fact]
        public async Task HandleAsync_DifferentUsers_ShouldUseCorrectUserId()
        {
            var message = CreateMessage("user-42");

            await _handler.HandleAsync(message);

            _cartService.Verify(s => s.ClearAsync(
                It.Is<PresaleUserId>(id => id.Value == "user-42"),
                It.IsAny<CancellationToken>()), Times.Once);
            _softReservationService.Verify(s => s.RemoveAllForUserAsync(
                "user-42",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        private static OrderPlaced CreateMessage(string userId)
            => new OrderPlaced(
                OrderId: 1,
                Items: new[] { new OrderPlacedItem(10, 2) },
                UserId: userId,
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: 100m,
                CurrencyId: 1);
    }
}
