using ECommerceApp.Application.Sales.Orders.Handlers;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderPaymentExpiredHandlerTests
    {
        private readonly Mock<IOrderRepository> _orderRepo;

        public OrderPaymentExpiredHandlerTests()
        {
            _orderRepo = new Mock<IOrderRepository>();
        }

        private OrderPaymentExpiredHandler CreateHandler()
            => new(_orderRepo.Object, NullLogger<OrderPaymentExpiredHandler>.Instance);

        private static PaymentExpired CreateMessage(int orderId = 1)
            => new(PaymentId: 10, OrderId: orderId, OccurredAt: DateTime.UtcNow);

        private static Order CreateOrder()
            => Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@example.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_OrderNotFound_ShouldNotCancelOrPublish()
        {
            _orderRepo
                .Setup(r => r.GetByIdWithItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order)null);

            await CreateHandler().HandleAsync(CreateMessage(), TestContext.Current.CancellationToken);

            _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_AlreadyCancelledOrder_ShouldNotPublish()
        {
            var order = CreateOrder();
            order.ExpirePayment();
            _orderRepo
                .Setup(r => r.GetByIdWithItemsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), TestContext.Current.CancellationToken);

            _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_AlreadyPaidOrder_ShouldNotCancelOrPublish()
        {
            var order = CreateOrder();
            order.ConfirmPayment(5);
            _orderRepo
                .Setup(r => r.GetByIdWithItemsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), TestContext.Current.CancellationToken);

            _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_PlacedOrder_ShouldExpirePaymentAndUpdate()
        {
            var order = CreateOrder();
            _orderRepo
                .Setup(r => r.GetByIdWithItemsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), TestContext.Current.CancellationToken);

            order.Status.Should().Be(OrderStatus.Cancelled);
            order.Events.Should().Contain(e => e.EventType == OrderEventType.OrderPaymentExpired);
            _orderRepo.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── State-machine guards (non-Placed statuses must be no-ops) ─────────

        [Fact]
        public async Task HandleAsync_FulfilledOrder_ShouldNotExpireOrUpdate()
        {
            var order = CreateOrder();
            order.ConfirmPayment(3);
            order.Fulfill();
            _orderRepo
                .Setup(r => r.GetByIdWithItemsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), TestContext.Current.CancellationToken);

            order.Status.Should().Be(OrderStatus.Fulfilled);
            _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_PaymentConfirmedOrder_ShouldNotExpireOrUpdate()
        {
            var order = CreateOrder();
            order.ConfirmPayment(7);
            _orderRepo
                .Setup(r => r.GetByIdWithItemsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), TestContext.Current.CancellationToken);

            order.Status.Should().Be(OrderStatus.PaymentConfirmed);
            _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── CorrelationId travels through message ──────────────────────────────

        [Fact]
        public async Task HandleAsync_PlacedOrder_ShouldAcceptNonDefaultCorrelationId()
        {
            var order = CreateOrder();
            _orderRepo
                .Setup(r => r.GetByIdWithItemsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
            var message = new PaymentExpired(PaymentId: 10, OrderId: 1, OccurredAt: DateTime.UtcNow,
                CorrelationId: Guid.NewGuid());

            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

            // Handler must still apply the state transition regardless of CorrelationId value.
            order.Status.Should().Be(OrderStatus.Cancelled);
            _orderRepo.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
