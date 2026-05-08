using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Payments
{
    public class OrderPlacedHandlerTests : BcBaseTest<IMessageBroker>
    {
        public OrderPlacedHandlerTests(ITestOutputHelper output) : base(output) { }

        private static OrderPlaced CreateMessage(
            int orderId = 1,
            decimal totalAmount = 150m,
            int currencyId = 1)
            => new(
                OrderId: orderId,
                Items: new List<OrderPlacedItem> { new(ProductId: 10, Quantity: 2) },
                UserId: "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e",
                ExpiresAt: DateTime.UtcNow.AddHours(24),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: totalAmount,
                CurrencyId: currencyId);

        // ── Payment creation ─────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_OrderPlaced_ShouldCreatePendingPayment()
        {
            await PublishAsync(CreateMessage(orderId: 1), CancellationToken);

            var payment = await GetRequiredService<IPaymentService>().GetByOrderIdAsync(1, CancellationToken);

            payment.ShouldNotBeNull();
            payment.OrderId.ShouldBe(1);
            payment.Status.ShouldBe("Pending");
        }

        [Fact]
        public async Task HandleAsync_OrderPlaced_ShouldCaptureCorrectTotalAmountAndCurrency()
        {
            await PublishAsync(CreateMessage(orderId: 2, totalAmount: 299.99m, currencyId: 2), CancellationToken);

            var payment = await GetRequiredService<IPaymentService>().GetByOrderIdAsync(2, CancellationToken);

            payment.ShouldNotBeNull();
            payment.TotalAmount.ShouldBe(299.99m);
            payment.CurrencyId.ShouldBe(2);
        }

        [Fact]
        public async Task HandleAsync_OrderPlaced_ConfirmedAtShouldBeNull()
        {
            await PublishAsync(CreateMessage(orderId: 3), CancellationToken);

            var payment = await GetRequiredService<IPaymentService>().GetByOrderIdAsync(3, CancellationToken);

            payment.ShouldNotBeNull();
            payment.ConfirmedAt.ShouldBeNull();
        }
    }
}

