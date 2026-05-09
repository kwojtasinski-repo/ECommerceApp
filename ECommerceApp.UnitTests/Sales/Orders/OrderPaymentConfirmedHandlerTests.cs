using ECommerceApp.Application.Sales.Orders.Handlers;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using AwesomeAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderPaymentConfirmedHandlerTests
    {
        private readonly Mock<IOrderRepository> _orderRepo;

        public OrderPaymentConfirmedHandlerTests()
        {
            _orderRepo = new Mock<IOrderRepository>();
        }

        private OrderPaymentConfirmedHandler CreateHandler()
            => new(_orderRepo.Object);

        private static PaymentConfirmed CreateMessage(int orderId = 1, int paymentId = 10)
            => new(paymentId, orderId, new List<PaymentConfirmedItem>(), DateTime.UtcNow);

        private static Order CreateOrder()
            => Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@example.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_OrderNotFound_ShouldNotUpdateRepository()
        {
            _orderRepo
                .Setup(r => r.GetByIdWithItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order)null);

            await CreateHandler().HandleAsync(CreateMessage(), TestContext.Current.CancellationToken);

            _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_AlreadyPaidOrder_ShouldNotUpdateRepository()
        {
            var order = CreateOrder();
            order.ConfirmPayment(5);
            _orderRepo
                .Setup(r => r.GetByIdWithItemsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1, paymentId: 10), TestContext.Current.CancellationToken);

            _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_UnpaidOrder_ShouldConfirmPaymentAndUpdate()
        {
            var order = CreateOrder();
            _orderRepo
                .Setup(r => r.GetByIdWithItemsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1, paymentId: 42), TestContext.Current.CancellationToken);

            order.Status.Should().Be(OrderStatus.PaymentConfirmed);
            order.Events.Should().Contain(e => e.EventType == OrderEventType.OrderPaymentConfirmed);
            _orderRepo.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
