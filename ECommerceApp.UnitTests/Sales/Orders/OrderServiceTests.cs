using AwesomeAssertions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders;
using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Domain.Shared;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderServiceTests
    {
        private readonly Mock<IOrderRepository> _orderRepo = new();
        private readonly Mock<IOrderItemRepository> _orderItemRepo = new();
        private readonly Mock<ICustomerExistenceChecker> _customerChecker = new();
        private readonly Mock<IOrderCustomerResolver> _customerResolver = new();
        private readonly Mock<IStockAvailabilityChecker> _stockChecker = new();
        private readonly Mock<IOrdersUnitOfWork> _uow = new();
        private readonly Mock<IOutboxWriter> _outboxWriter = new();
        private readonly Mock<IImageUrlBuilder> _urlBuilder = new();

        public OrderServiceTests()
        {
            _stockChecker
                .Setup(c => c.FindInsufficientAsync(It.IsAny<IReadOnlyList<OrderPlacedItem>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<int>());
        }

        private OrderService CreateService()
            => new(_orderRepo.Object, _orderItemRepo.Object, _customerChecker.Object,
                _customerResolver.Object, _stockChecker.Object, _uow.Object, _outboxWriter.Object, _urlBuilder.Object);

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@example.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        private static OrderItem CreateCartItem(int itemId, int quantity, decimal unitCost, string userId)
            => OrderItem.Create(new OrderProductId(itemId), quantity, new UnitCost(unitCost), userId);

        private void SetupOrderLookup(int orderId, Order order)
            => _orderRepo.Setup(r => r.GetByIdWithItemsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        private void SetupOrderUpdate(Order order)
            => _orderRepo.Setup(r => r.UpdateAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        private void SetupOrderAdd(int orderId)
            => _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(orderId);

        private Mock<IOutboxTransaction> SetupTransaction()
        {
            var txMock = new Mock<IOutboxTransaction>();
            _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return txMock;
        }

        // ── PlaceOrderFromPresaleAsync ───────────────────────────────────────

        [Fact]
        public async Task PlaceOrderFromPresaleAsync_ValidLines_EnqueuesOrderPlacedAndCommits()
        {
            // Arrange
            var txMock = SetupTransaction();
            SetupOrderAdd(777);

            var svc = CreateService();
            var dto = new PlaceOrderFromPresaleDto(
                CustomerId: 1,
                CurrencyId: 1,
                UserId: "user-1",
                Customer: new OrderCustomerData("Jan", "Kowalski", "jan@example.com", "123456789",
                    false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "PL"),
                Lines: new List<PlaceOrderLineDto> { new(ProductId: 55, Quantity: 3, UnitPrice: 10m) });

            // Act
            var result = await svc.PlaceOrderFromPresaleAsync(dto);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.OrderId.Should().Be(777);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<OrderPlaced>(m => m.OrderId == 777 && m.UserId == "user-1" && m.TotalAmount == 30m),
                txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PlaceOrderFromPresaleAsync_InsufficientStock_ReturnsInsufficientStock_AndDoesNotOpenTransaction()
        {
            // Arrange
            _stockChecker
                .Setup(c => c.FindInsufficientAsync(It.IsAny<IReadOnlyList<OrderPlacedItem>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<int> { 55 });

            var svc = CreateService();
            var dto = new PlaceOrderFromPresaleDto(
                CustomerId: 1,
                CurrencyId: 1,
                UserId: "user-1",
                Customer: new OrderCustomerData("Jan", "Kowalski", "jan@example.com", "123456789",
                    false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "PL"),
                Lines: new List<PlaceOrderLineDto> { new(ProductId: 55, Quantity: 3, UnitPrice: 10m) });

            // Act
            var result = await svc.PlaceOrderFromPresaleAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.FailureReason.Should().Contain("55");
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
            _orderRepo.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PlaceOrderFromPresaleAsync_NoLines_ReturnsCartItemsNotFound_AndDoesNotOpenTransaction()
        {
            // Arrange
            var svc = CreateService();
            var dto = new PlaceOrderFromPresaleDto(
                CustomerId: 1,
                CurrencyId: 1,
                UserId: "user-1",
                Customer: new OrderCustomerData("Jan", "Kowalski", "jan@example.com", "123456789",
                    false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "PL"),
                Lines: new List<PlaceOrderLineDto>());

            // Act
            var result = await svc.PlaceOrderFromPresaleAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── MarkAsDeliveredAsync ─────────────────────────────────────────────

        [Fact]
        public async Task MarkAsDeliveredAsync_PaymentConfirmedOrder_EnqueuesOrderShippedAndCommits()
        {
            // Arrange
            var order = Order.Create(1, 1, "user-1", OrderNumber.Generate(), CreateCustomer());
            order.AddItem(CreateCartItem(10, 2, 25m, "user-1"));
            order.ConfirmPayment(1);
            var txMock = SetupTransaction();

            SetupOrderLookup(42, order);
            SetupOrderUpdate(order);

            var svc = CreateService();
            // Act
            var result = await svc.MarkAsDeliveredAsync(42);

            // Assert
            result.Should().Be(OrderOperationResult.Success);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<OrderShipped>(m => m.OrderId == 42),
                txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_NonExistentOrder_ReturnsOrderNotFound_AndDoesNotOpenTransaction()
        {
            // Arrange
            SetupOrderLookup(999, null);

            var svc = CreateService();
            // Act
            var result = await svc.MarkAsDeliveredAsync(999);

            // Assert
            result.Should().Be(OrderOperationResult.OrderNotFound);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_NotYetPaid_ReturnsNotPaid_AndDoesNotOpenTransaction()
        {
            // Arrange
            var order = Order.Create(1, 1, "user-1", OrderNumber.Generate(), CreateCustomer());
            SetupOrderLookup(42, order);

            var svc = CreateService();
            // Act
            var result = await svc.MarkAsDeliveredAsync(42);

            // Assert
            result.Should().Be(OrderOperationResult.NotPaid);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── CancelOrderAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task CancelOrderAsync_PlacedOrder_EnqueuesOrderCancelledAndCommits()
        {
            // Arrange
            var order = Order.Create(1, 1, "user-1", OrderNumber.Generate(), CreateCustomer());
            order.AddItem(CreateCartItem(10, 2, 25m, "user-1"));
            var txMock = SetupTransaction();

            SetupOrderLookup(42, order);
            SetupOrderUpdate(order);

            var svc = CreateService();
            // Act
            var result = await svc.CancelOrderAsync(42);

            // Assert
            result.Should().Be(OrderOperationResult.Success);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<OrderCancelled>(m => m.OrderId == 42 && m.Items.Count == 1),
                txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelOrderAsync_AlreadyCancelledOrder_ReturnsAlreadyCancelled_AndDoesNotOpenTransaction()
        {
            // Arrange
            var order = Order.Create(1, 1, "user-1", OrderNumber.Generate(), CreateCustomer());
            order.Cancel("test");
            SetupOrderLookup(42, order);

            var svc = CreateService();
            // Act
            var result = await svc.CancelOrderAsync(42);

            // Assert
            result.Should().Be(OrderOperationResult.AlreadyCancelled);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CancelOrderAsync_NonExistentOrder_ReturnsOrderNotFound_AndDoesNotOpenTransaction()
        {
            // Arrange
            SetupOrderLookup(999, null);

            var svc = CreateService();
            // Act
            var result = await svc.CancelOrderAsync(999);

            // Assert
            result.Should().Be(OrderOperationResult.OrderNotFound);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
