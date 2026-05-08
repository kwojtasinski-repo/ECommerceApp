using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Orders
{
    public class OrderPaymentConfirmedHandlerTests : BcBaseTest<IMessageBroker>
    {
        public OrderPaymentConfirmedHandlerTests(ITestOutputHelper output) : base(output) { }

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@test.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        private async Task<int> SeedOrderAsync(CancellationToken ct = default)
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(1, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            return await repo.AddAsync(order);
        }

        // ── PaymentConfirmed → Order status ──────────────────────────────

        [Fact]
        public async Task HandleAsync_PaymentConfirmed_ShouldTransitionOrderToPaymentConfirmed()
        {
            var orderId = await SeedOrderAsync(ct: CancellationToken);
            var message = new PaymentConfirmed(
                PaymentId: 10,
                OrderId: orderId,
                Items: new List<PaymentConfirmedItem>(),
                OccurredAt: DateTime.UtcNow);

            await PublishAsync(message, CancellationToken);

            var order = await GetRequiredService<IOrderService>().GetOrderDetailsAsync(orderId, CancellationToken);
            order.ShouldNotBeNull();
            order.Status.ShouldBe(OrderStatus.PaymentConfirmed);
        }

        [Fact]
        public async Task HandleAsync_PaymentConfirmed_NonExistentOrder_ShouldNotThrow()
        {
            var message = new PaymentConfirmed(
                PaymentId: 10,
                OrderId: int.MaxValue,
                Items: new List<PaymentConfirmedItem>(),
                OccurredAt: DateTime.UtcNow);

            await PublishAsync(message, CancellationToken);
        }
    }
}

