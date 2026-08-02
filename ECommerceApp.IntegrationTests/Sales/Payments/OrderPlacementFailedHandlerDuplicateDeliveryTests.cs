using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Domain.Sales.Payments;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Payments
{
    /// <summary>
    /// Phase 4 Inbox-idempotency proof for <c>OrderPlacementFailedHandler</c> (Sales/Payments) — audited
    /// "needs dedup" because <c>Payment.Cancel()</c> throws <see cref="ECommerceApp.Domain.Shared.DomainException"/>
    /// if the payment isn't <c>Pending</c> anymore, so an undeduped redelivery would crash rather than
    /// no-op. Asserts both that the payment ends up <c>Cancelled</c> exactly once AND that the second
    /// delivery doesn't throw.
    /// </summary>
    public class OrderPlacementFailedHandlerDuplicateDeliveryTests : BcBaseTest<IPaymentService>
    {
        public OrderPlacementFailedHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        private async Task<int> SeedPaymentAsync(int orderId, CancellationToken ct = default)
        {
            var repo = GetRequiredService<IPaymentRepository>();
            var payment = Payment.Create(new PaymentOrderId(orderId), 100m, 1, DateTime.UtcNow.AddHours(24), PROPER_CUSTOMER_ID);
            await repo.AddAsync(payment, ct);
            var seeded = await repo.GetByOrderIdAsync(orderId, ct);
            return seeded!.Id.Value;
        }

        [Fact]
        public async Task RedeliverAsync_SameOrderPlacementFailed_ShouldCancelPaymentExactlyOnceAndNotThrow()
        {
            const int orderId = 1;
            await SeedPaymentAsync(orderId, CancellationToken);

            var message = new OrderPlacementFailed(
                OrderId: orderId,
                Reason: "InsufficientStock",
                Items: new List<OrderPlacedItem> { new(ProductId: 1, Quantity: 1) },
                UserId: PROPER_CUSTOMER_ID);

            await RedeliverAsync(message, outboxMessageId: 920001, CancellationToken);
            await RedeliverAsync(message, outboxMessageId: 920001, CancellationToken);

            var payment = await _service.GetByOrderIdAsync(orderId, CancellationToken);
            payment.ShouldNotBeNull();
            payment!.Status.ShouldBe("Cancelled");
        }
    }
}
