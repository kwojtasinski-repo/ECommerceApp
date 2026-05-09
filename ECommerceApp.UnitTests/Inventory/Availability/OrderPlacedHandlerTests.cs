using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Sales.Orders.Messages;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class OrderPlacedHandlerTests
    {
        private readonly Mock<IStockService> _stockService;
        private readonly OrderPlacedHandler _handler;

        public OrderPlacedHandlerTests()
        {
            _stockService = new Mock<IStockService>();
            _handler = new OrderPlacedHandler(_stockService.Object);
        }

        [Fact]
        public async Task HandleAsync_MultipleItems_ShouldReserveEachItem()
        {
            var items = new[]
            {
                new OrderPlacedItem(10, 2),
                new OrderPlacedItem(20, 5)
            };
            var expiresAt = DateTime.UtcNow.AddHours(1);
            var message = new OrderPlaced(
                OrderId: 1,
                Items: items,
                UserId: "user-1",
                ExpiresAt: expiresAt,
                OccurredAt: DateTime.UtcNow,
                TotalAmount: 100m,
                CurrencyId: 1);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReserveAsync(
                It.Is<ReserveStockDto>(d =>
                    d.ProductId == 10 &&
                    d.OrderId == 1 &&
                    d.Quantity == 2 &&
                    d.UserId == "user-1" &&
                    d.ExpiresAt == expiresAt),
                It.IsAny<CancellationToken>()), Times.Once);

            _stockService.Verify(s => s.ReserveAsync(
                It.Is<ReserveStockDto>(d =>
                    d.ProductId == 20 &&
                    d.OrderId == 1 &&
                    d.Quantity == 5 &&
                    d.UserId == "user-1" &&
                    d.ExpiresAt == expiresAt),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_SingleItem_ShouldReserveOnce()
        {
            var items = new[] { new OrderPlacedItem(30, 1) };
            var message = new OrderPlaced(
                OrderId: 7,
                Items: items,
                UserId: "user-2",
                ExpiresAt: DateTime.UtcNow.AddHours(2),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: 50m,
                CurrencyId: 1);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReserveAsync(
                It.Is<ReserveStockDto>(d => d.ProductId == 30 && d.OrderId == 7 && d.Quantity == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_EmptyItems_ShouldNotCallReserve()
        {
            var message = new OrderPlaced(
                OrderId: 1,
                Items: Array.Empty<OrderPlacedItem>(),
                UserId: "user-3",
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: 0m,
                CurrencyId: 1);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReserveAsync(
                It.IsAny<ReserveStockDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
