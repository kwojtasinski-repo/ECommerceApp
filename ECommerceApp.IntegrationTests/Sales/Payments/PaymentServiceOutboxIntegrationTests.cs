using ECommerceApp.Application.Sales.Payments.DTOs;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Domain.Sales.Payments;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Payments
{
    public class PaymentServiceOutboxIntegrationTests : BcBaseTest<IPaymentService>
    {
        public PaymentServiceOutboxIntegrationTests(ITestOutputHelper output) : base(output) { }

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@test.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        private async Task<int> SeedOrderAsync(CancellationToken ct = default)
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(1, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            return await repo.AddAsync(order);
        }

        private async Task<int> SeedPaymentAsync(int orderId, CancellationToken ct = default)
        {
            var repo = GetRequiredService<IPaymentRepository>();
            var payment = Payment.Create(new PaymentOrderId(orderId), 100m, 1, DateTime.UtcNow.AddHours(24), PROPER_CUSTOMER_ID);
            await repo.AddAsync(payment, ct);
            var seeded = await repo.GetByOrderIdAsync(orderId, ct);
            return seeded!.Id.Value;
        }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition())
                    return;

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        // Reads the order through a brand-new IServiceScope/DbContext each call — the background
        // OutboxDispatcher applies its update through its own scope, and this test's root-resolved
        // IOrderRepository would otherwise keep returning the stale, already-tracked Order instance
        // from SeedOrderAsync instead of re-querying the shared InMemory store.
        private async Task<OrderStatus> GetOrderStatusAsync(int orderId)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var order = await repo.GetByIdWithItemsAsync(orderId, CancellationToken);
            return order!.Status;
        }

        [Fact]
        public async Task ConfirmAsync_EnqueuesOutboxMessage_AndOrderEventuallyTransitionsToPaymentConfirmed()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var paymentId = await SeedPaymentAsync(orderId, CancellationToken);

            var result = await _service.ConfirmAsync(new ConfirmPaymentDto(paymentId, "TX-OUTBOX"), CancellationToken);
            result.ShouldBe(PaymentOperationResult.Success);

            await WaitUntilAsync(
                async () => await GetOrderStatusAsync(orderId) == OrderStatus.PaymentConfirmed,
                TimeSpan.FromSeconds(20));

            (await GetOrderStatusAsync(orderId)).ShouldBe(OrderStatus.PaymentConfirmed);
        }
    }
}
