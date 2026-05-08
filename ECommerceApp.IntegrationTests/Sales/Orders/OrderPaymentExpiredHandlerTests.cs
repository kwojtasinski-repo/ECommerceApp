using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Orders
{
    public class OrderPaymentExpiredHandlerTests : BcBaseTest<IMessageBroker>
    {
        public OrderPaymentExpiredHandlerTests(ITestOutputHelper output) : base(output) { }

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@test.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        private async Task<int> SeedOrderAsync(CancellationToken ct = default)
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(1, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            return await repo.AddAsync(order);
        }

        // ── PaymentExpired → Order status ─────────────────────────────────

        [Fact]
        public async Task HandleAsync_PaymentExpired_ShouldTransitionOrderToCancelled()
        {
            var orderId = await SeedOrderAsync(ct: CancellationToken);
            var message = new PaymentExpired(
                PaymentId: 10,
                OrderId: orderId,
                OccurredAt: DateTime.UtcNow);

            await PublishAsync(message, CancellationToken);

            var order = await GetRequiredService<IOrderService>().GetOrderDetailsAsync(orderId, CancellationToken);
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

            await PublishAsync(message, CancellationToken);
        }
    }
}

