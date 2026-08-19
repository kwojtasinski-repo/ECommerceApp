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
        private readonly Mock<IOrdersUnitOfWork> _uow = new();
        private readonly Mock<IOutboxWriter> _outboxWriter = new();
        private readonly Mock<IImageUrlBuilder> _urlBuilder = new();

        private OrderService CreateService()
            => new(_orderRepo.Object, _orderItemRepo.Object, _customerChecker.Object,
                _customerResolver.Object, _uow.Object, _outboxWriter.Object, _urlBuilder.Object);

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@example.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        private static OrderItem CreateCartItem(int itemId, int quantity, decimal unitCost, string userId)
            => OrderItem.Create(new OrderProductId(itemId), quantity, new UnitCost(unitCost), userId);

        private void SetupCustomerExists(int customerId, bool exists)
            => _customerChecker.Setup(c => c.ExistsAsync(customerId, It.IsAny<CancellationToken>())).ReturnsAsync(exists);

        private void SetupCartItems(IReadOnlyList<OrderItem> cartItems)
            => _orderItemRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cartItems);

        private void SetupOrderLookup(int orderId, Order order)
            => _orderRepo.Setup(r => r.GetByIdWithItemsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        private void SetupOrderUpdate(Order order)
            => _orderRepo.Setup(r => r.UpdateAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        private void SetupOrderAdd(int orderId)
            => _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(orderId);

        private void SetupPlacementFailurePublishing(Mock<IOutboxTransaction> txMock)
        {
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<OrderPlaced>(), txMock.Object, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("serialization boom"));
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<OrderPlacementFailed>(), txMock.Object, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private Order SetupValidOrderPlacement(int orderId, string userId, IReadOnlyList<OrderItem> cartItems)
        {
            _customerChecker.Setup(c => c.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _orderItemRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cartItems);
            _customerResolver.Setup(r => r.ResolveAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(CreateCustomer());
            _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(orderId);
            _orderItemRepo.Setup(r => r.AssignToOrderAsync(It.IsAny<IReadOnlyList<int>>(), orderId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var placedOrder = Order.Create(1, 1, userId, OrderNumber.Generate(), CreateCustomer());
            foreach (var cartItem in cartItems)
                placedOrder.AddItem(cartItem);

            _orderRepo.Setup(r => r.GetByIdWithItemsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(placedOrder);
            _orderRepo.Setup(r => r.UpdateAsync(placedOrder, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            return placedOrder;
        }

        private Mock<IOutboxTransaction> SetupTransaction()
        {
            var txMock = new Mock<IOutboxTransaction>();
            _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return txMock;
        }

        // ── PlaceOrderAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task PlaceOrderAsync_ValidOrder_EnqueuesOrderPlacedAndCommits()
        {
            // Arrange
            const string userId = "user-1";
            var cartItems = new List<OrderItem> { CreateCartItem(10, 2, 25m, userId) };
            var txMock = SetupTransaction();
            var placedOrder = SetupValidOrderPlacement(501, userId, cartItems);

            var svc = CreateService();
            var dto = new PlaceOrderDto(CustomerId: 1, CurrencyId: 1, UserId: userId, CartItemIds: new List<int> { 10 });

            // Act
            var result = await svc.PlaceOrderAsync(dto);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.OrderId.Should().Be(501);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<OrderPlaced>(m => m.OrderId == 501 && m.UserId == userId && m.TotalAmount == placedOrder.Cost),
                txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PlaceOrderAsync_NonExistentCustomer_ReturnsCustomerNotFound_AndDoesNotOpenTransaction()
        {
            // Arrange
            SetupCustomerExists(999, false);

            var svc = CreateService();
            var dto = new PlaceOrderDto(CustomerId: 999, CurrencyId: 1, UserId: "user-1", CartItemIds: new List<int> { 10 });

            // Act
            var result = await svc.PlaceOrderAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.CustomerId.Should().Be(999);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_NoCartItemsFound_ReturnsCartItemsNotFound()
        {
            // Arrange
            SetupCustomerExists(1, true);
            SetupCartItems(new List<OrderItem>());

            var svc = CreateService();
            var dto = new PlaceOrderDto(CustomerId: 1, CurrencyId: 1, UserId: "user-1", CartItemIds: new List<int> { 10 });

            // Act
            var result = await svc.PlaceOrderAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_CartItemsOwnedByDifferentUser_ReturnsCartItemsNotOwnedByUser()
        {
            // Arrange
            var cartItems = new List<OrderItem> { CreateCartItem(10, 2, 25m, "other-user") };
            SetupCustomerExists(1, true);
            SetupCartItems(cartItems);

            var svc = CreateService();
            var dto = new PlaceOrderDto(CustomerId: 1, CurrencyId: 1, UserId: "user-1", CartItemIds: new List<int> { 10 });

            // Act
            var result = await svc.PlaceOrderAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_OutboxEnqueueFailsForOrderPlaced_EnqueuesOrderPlacementFailed_CommitsAndReturnsPlacementFailed()
        {
            // Arrange
            const string userId = "user-1";
            var cartItems = new List<OrderItem> { CreateCartItem(10, 2, 25m, userId) };
            var txMock = SetupTransaction();
            var placedOrder = SetupValidOrderPlacement(501, userId, cartItems);

            SetupPlacementFailurePublishing(txMock);

            var svc = CreateService();
            var dto = new PlaceOrderDto(CustomerId: 1, CurrencyId: 1, UserId: userId, CartItemIds: new List<int> { 10 });

            // Act
            var result = await svc.PlaceOrderAsync(dto);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.OrderId.Should().Be(501);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<OrderPlacementFailed>(m => m.OrderId == 501 && m.UserId == userId),
                txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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
