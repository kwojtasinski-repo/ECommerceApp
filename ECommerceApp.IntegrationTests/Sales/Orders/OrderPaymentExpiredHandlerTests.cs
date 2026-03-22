using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Orders
{
    public class OrderPaymentExpiredHandlerTests : BcBaseTest<IMessageBroker>
    {
        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@test.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        private async Task<int> SeedOrderAsync()
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(1, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            return await repo.AddAsync(order);
        }

        // ── PaymentExpired → Order status ─────────────────────────────────

        [Fact]
        public async Task HandleAsync_PaymentExpired_ShouldTransitionOrderToCancelled()
        {
            var orderId = await SeedOrderAsync();
            var message = new PaymentExpired(
                PaymentId: 10,
                OrderId: orderId,
                OccurredAt: DateTime.UtcNow);

            await _service.PublishAsync(message);

            var order = await GetRequiredService<IOrderService>().GetOrderDetailsAsync(orderId);
            order.ShouldNotBeNull();
            order.Status.ShouldBe(OrderStatus.Cancelled);
        }

        [Fact]
        public async Task HandleAsync_PaymentExpired_NonExistentOrder_ShouldNotThrow()
        {
            var message = new PaymentExpired(
                PaymentId: 10,
                OrderId: int.MaxValue,
                OccurredAt: DateTime.UtcNow);

            await _service.PublishAsync(message);
        }
    }
}
