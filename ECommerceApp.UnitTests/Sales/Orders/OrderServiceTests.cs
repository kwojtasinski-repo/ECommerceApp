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
            const string userId = "user-1";
            var cartItems = new List<OrderItem> { CreateCartItem(10, 2, 25m, userId) };
            var txMock = SetupTransaction();

            _customerChecker.Setup(c => c.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _orderItemRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cartItems);
            _customerResolver.Setup(r => r.ResolveAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(CreateCustomer());
            _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(501);
            _orderItemRepo.Setup(r => r.AssignToOrderAsync(It.IsAny<IReadOnlyList<int>>(), 501, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var placedOrder = Order.Create(1, 1, userId, OrderNumber.Generate(), CreateCustomer());
            placedOrder.AddItem(CreateCartItem(10, 2, 25m, userId));
            _orderRepo.Setup(r => r.GetByIdWithItemsAsync(501, It.IsAny<CancellationToken>())).ReturnsAsync(placedOrder);
            _orderRepo.Setup(r => r.UpdateAsync(placedOrder, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var svc = CreateService();
            var dto = new PlaceOrderDto(CustomerId: 1, CurrencyId: 1, UserId: userId, CartItemIds: new List<int> { 10 });

            var result = await svc.PlaceOrderAsync(dto);

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
            _customerChecker.Setup(c => c.ExistsAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var svc = CreateService();
            var dto = new PlaceOrderDto(CustomerId: 999, CurrencyId: 1, UserId: "user-1", CartItemIds: new List<int> { 10 });

            var result = await svc.PlaceOrderAsync(dto);

            result.IsSuccess.Should().BeFalse();
            result.CustomerId.Should().Be(999);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_NoCartItemsFound_ReturnsCartItemsNotFound()
        {
            _customerChecker.Setup(c => c.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _orderItemRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderItem>());

            var svc = CreateService();
            var dto = new PlaceOrderDto(CustomerId: 1, CurrencyId: 1, UserId: "user-1", CartItemIds: new List<int> { 10 });

            var result = await svc.PlaceOrderAsync(dto);

            result.IsSuccess.Should().BeFalse();
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_CartItemsOwnedByDifferentUser_ReturnsCartItemsNotOwnedByUser()
        {
            var cartItems = new List<OrderItem> { CreateCartItem(10, 2, 25m, "other-user") };
            _customerChecker.Setup(c => c.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _orderItemRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cartItems);

            var svc = CreateService();
            var dto = new PlaceOrderDto(CustomerId: 1, CurrencyId: 1, UserId: "user-1", CartItemIds: new List<int> { 10 });

            var result = await svc.PlaceOrderAsync(dto);

            result.IsSuccess.Should().BeFalse();
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PlaceOrderAsync_OutboxEnqueueFailsForOrderPlaced_EnqueuesOrderPlacementFailed_CommitsAndReturnsPlacementFailed()
        {
            const string userId = "user-1";
            var cartItems = new List<OrderItem> { CreateCartItem(10, 2, 25m, userId) };

            var txMock = new Mock<IOutboxTransaction>();
            _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _customerChecker.Setup(c => c.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _orderItemRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cartItems);
            _customerResolver.Setup(r => r.ResolveAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(CreateCustomer());
            _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(501);
            _orderItemRepo.Setup(r => r.AssignToOrderAsync(It.IsAny<IReadOnlyList<int>>(), 501, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var placedOrder = Order.Create(1, 1, userId, OrderNumber.Generate(), CreateCustomer());
            placedOrder.AddItem(CreateCartItem(10, 2, 25m, userId));
            _orderRepo.Setup(r => r.GetByIdWithItemsAsync(501, It.IsAny<CancellationToken>())).ReturnsAsync(placedOrder);
            _orderRepo.Setup(r => r.UpdateAsync(placedOrder, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<OrderPlaced>(), txMock.Object, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("serialization boom"));
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<OrderPlacementFailed>(), txMock.Object, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var svc = CreateService();
            var dto = new PlaceOrderDto(CustomerId: 1, CurrencyId: 1, UserId: userId, CartItemIds: new List<int> { 10 });

            var result = await svc.PlaceOrderAsync(dto);

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
            var txMock = SetupTransaction();
            _orderRepo.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(777);

            var svc = CreateService();
            var dto = new PlaceOrderFromPresaleDto(
                CustomerId: 1,
                CurrencyId: 1,
                UserId: "user-1",
                Customer: new OrderCustomerData("Jan", "Kowalski", "jan@example.com", "123456789",
                    false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "PL"),
                Lines: new List<PlaceOrderLineDto> { new(ProductId: 55, Quantity: 3, UnitPrice: 10m) });

            var result = await svc.PlaceOrderFromPresaleAsync(dto);

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
            var svc = CreateService();
            var dto = new PlaceOrderFromPresaleDto(
                CustomerId: 1,
                CurrencyId: 1,
                UserId: "user-1",
                Customer: new OrderCustomerData("Jan", "Kowalski", "jan@example.com", "123456789",
                    false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "PL"),
                Lines: new List<PlaceOrderLineDto>());

            var result = await svc.PlaceOrderFromPresaleAsync(dto);

            result.IsSuccess.Should().BeFalse();
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── MarkAsDeliveredAsync ─────────────────────────────────────────────

        [Fact]
        public async Task MarkAsDeliveredAsync_PaymentConfirmedOrder_EnqueuesOrderShippedAndCommits()
        {
            var order = Order.Create(1, 1, "user-1", OrderNumber.Generate(), CreateCustomer());
            order.AddItem(CreateCartItem(10, 2, 25m, "user-1"));
            order.ConfirmPayment(1);
            var txMock = SetupTransaction();

            _orderRepo.Setup(r => r.GetByIdWithItemsAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(order);
            _orderRepo.Setup(r => r.UpdateAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var svc = CreateService();
            var result = await svc.MarkAsDeliveredAsync(42);

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
            _orderRepo.Setup(r => r.GetByIdWithItemsAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Order)null);

            var svc = CreateService();
            var result = await svc.MarkAsDeliveredAsync(999);

            result.Should().Be(OrderOperationResult.OrderNotFound);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkAsDeliveredAsync_NotYetPaid_ReturnsNotPaid_AndDoesNotOpenTransaction()
        {
            var order = Order.Create(1, 1, "user-1", OrderNumber.Generate(), CreateCustomer());
            _orderRepo.Setup(r => r.GetByIdWithItemsAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(order);

            var svc = CreateService();
            var result = await svc.MarkAsDeliveredAsync(42);

            result.Should().Be(OrderOperationResult.NotPaid);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── CancelOrderAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task CancelOrderAsync_PlacedOrder_EnqueuesOrderCancelledAndCommits()
        {
            var order = Order.Create(1, 1, "user-1", OrderNumber.Generate(), CreateCustomer());
            order.AddItem(CreateCartItem(10, 2, 25m, "user-1"));
            var txMock = SetupTransaction();

            _orderRepo.Setup(r => r.GetByIdWithItemsAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(order);
            _orderRepo.Setup(r => r.UpdateAsync(order, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var svc = CreateService();
            var result = await svc.CancelOrderAsync(42);

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
            var order = Order.Create(1, 1, "user-1", OrderNumber.Generate(), CreateCustomer());
            order.Cancel("test");
            _orderRepo.Setup(r => r.GetByIdWithItemsAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(order);

            var svc = CreateService();
            var result = await svc.CancelOrderAsync(42);

            result.Should().Be(OrderOperationResult.AlreadyCancelled);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CancelOrderAsync_NonExistentOrder_ReturnsOrderNotFound_AndDoesNotOpenTransaction()
        {
            _orderRepo.Setup(r => r.GetByIdWithItemsAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Order)null);

            var svc = CreateService();
            var result = await svc.CancelOrderAsync(999);

            result.Should().Be(OrderOperationResult.OrderNotFound);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
