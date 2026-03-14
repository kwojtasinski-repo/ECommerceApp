using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Presale.Checkout;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public async Task HandleAsync_OrderPlaced_ShouldRemoveOnlyOrderedProductsFromCart()
        {
            var message = CreateMessage("user-1", new OrderPlacedItem(10, 2), new OrderPlacedItem(20, 1));

            await _handler.HandleAsync(message);

            _cartService.Verify(s => s.RemoveRangeAsync(
                It.Is<PresaleUserId>(id => id.Value == "user-1"),
                It.Is<IReadOnlyList<PresaleProductId>>(ids =>
                    ids.Count == 2 &&
                    ids.Any(p => p.Value == 10) &&
                    ids.Any(p => p.Value == 20)),
                It.IsAny<CancellationToken>()), Times.Once);
            _cartService.Verify(s => s.ClearAsync(
                It.IsAny<PresaleUserId>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_OrderPlaced_ShouldRemoveCommittedSoftReservationsForUser()
        {
            var message = CreateMessage("user-1");

            await _handler.HandleAsync(message);

            _softReservationService.Verify(s => s.RemoveCommittedForUserAsync(
                "user-1",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_OrderPlaced_ShouldRemoveCartItemsBeforeRemovingReservations()
        {
            var callOrder = new List<string>();
            _cartService.Setup(s => s.RemoveRangeAsync(It.IsAny<PresaleUserId>(), It.IsAny<IReadOnlyList<PresaleProductId>>(), It.IsAny<CancellationToken>()))
                .Callback(() => callOrder.Add("cart"))
                .Returns(Task.CompletedTask);
            _softReservationService.Setup(s => s.RemoveCommittedForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

            _cartService.Verify(s => s.RemoveRangeAsync(
                It.Is<PresaleUserId>(id => id.Value == "user-42"),
                It.Is<IReadOnlyList<PresaleProductId>>(ids => ids.Any(p => p.Value == 10)),
                It.IsAny<CancellationToken>()), Times.Once);
            _softReservationService.Verify(s => s.RemoveCommittedForUserAsync(
                "user-42",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        private static OrderPlaced CreateMessage(string userId, params OrderPlacedItem[] items)
        {
            var resolvedItems = items.Length > 0 ? items : new[] { new OrderPlacedItem(10, 2) };
            return new OrderPlaced(
                OrderId: 1,
                Items: resolvedItems,
                UserId: userId,
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: 100m,
                CurrencyId: 1);
        }
    }
}
