using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Orders
{
    /// <summary>
    /// Phase 4 Inbox-idempotency proof for the 5 Sales/Orders handlers audited "needs dedup" (excluding
    /// the 2 explicitly-excluded placeholder handlers, <c>OrderCouponAppliedHandler</c>/
    /// <c>OrderPriceAdjustedHandler</c> — see the plan file's scope-exclusion note). Each of these appends
    /// an <see cref="OrderEvent"/> with no natural guard, so redelivery without dedup would append a
    /// second duplicate event — asserted here via the real, persisted <c>Order.Events</c> collection.
    /// </summary>
    public class SalesOrdersDuplicateDeliveryTests : BcBaseTest<IMessageBroker>
    {
        public SalesOrdersDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@test.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        private async Task<int> SeedOrderAsync(CancellationToken ct = default)
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(1, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            return await repo.AddAsync(order, ct);
        }

        /// <summary>
        /// <c>OrderShipmentDispatchedHandler</c>/<c>OrderShipmentPartiallyDeliveredHandler</c> both guard
        /// on <c>Status == PaymentConfirmed</c> (<c>Order.RecordShipmentDispatched</c>/
        /// <c>MarkAsPartiallyFulfilled</c>) — a freshly-<c>Order.Create</c>d order starts at
        /// <c>Placed</c>, so their event append would silently no-op without this transition first.
        /// </summary>
        private async Task<int> SeedPaymentConfirmedOrderAsync(CancellationToken ct = default)
        {
            var order = Order.Create(1, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            var orderId = await GetRequiredService<IOrderRepository>().AddAsync(order, ct);

            // Fresh repo/DbContext instance per step — reusing the same one that just Add()-ed `order`
            // and then Update()-ing a separately-fetched instance with the same key throws an EF identity
            // conflict ("already being tracked"), the write-side counterpart of the stale-read gotcha the
            // Inventory tests hit.
            var reloaded = await GetRequiredService<IOrderRepository>().GetByIdAsync(orderId, ct);
            reloaded.ConfirmPayment(1);
            await GetRequiredService<IOrderRepository>().UpdateAsync(reloaded, ct);
            return orderId;
        }

        private async Task<Order> ReloadOrderAsync(int orderId, CancellationToken ct = default)
            => await GetRequiredService<IOrderRepository>().GetByIdWithItemsAsync(orderId, ct);

        public class OrderShipmentDispatchedHandlerDuplicateDeliveryTests : SalesOrdersDuplicateDeliveryTests
        {
            public OrderShipmentDispatchedHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

            [Fact]
            public async Task RedeliverAsync_SameShipmentDispatched_ShouldAppendEventExactlyOnce()
            {
                var orderId = await SeedPaymentConfirmedOrderAsync(CancellationToken);
                var message = new ShipmentDispatched(ShipmentId: 1, OrderId: orderId, TrackingNumber: "TRACK-1", OccurredAt: DateTime.UtcNow);

                await RedeliverAsync(message, outboxMessageId: 910001, CancellationToken);
                await RedeliverAsync(message, outboxMessageId: 910001, CancellationToken);

                var order = await ReloadOrderAsync(orderId, CancellationToken);
                order.Events.Count(e => e.EventType == OrderEventType.ShipmentDispatched).ShouldBe(1);
            }
        }

        public class OrderShipmentFailedHandlerDuplicateDeliveryTests : SalesOrdersDuplicateDeliveryTests
        {
            public OrderShipmentFailedHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

            [Fact]
            public async Task RedeliverAsync_SameShipmentFailed_ShouldAppendEventExactlyOnce()
            {
                var orderId = await SeedOrderAsync(CancellationToken);
                var message = new ShipmentFailed(
                    ShipmentId: 1,
                    OrderId: orderId,
                    Items: new List<ShipmentLineItem> { new(ProductId: 1, Quantity: 1) },
                    OccurredAt: DateTime.UtcNow);

                await RedeliverAsync(message, outboxMessageId: 910002, CancellationToken);
                await RedeliverAsync(message, outboxMessageId: 910002, CancellationToken);

                var order = await ReloadOrderAsync(orderId, CancellationToken);
                order.Events.Count(e => e.EventType == OrderEventType.ShipmentFailed).ShouldBe(1);
            }
        }

        public class OrderShipmentPartiallyDeliveredHandlerDuplicateDeliveryTests : SalesOrdersDuplicateDeliveryTests
        {
            public OrderShipmentPartiallyDeliveredHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

            [Fact]
            public async Task RedeliverAsync_SameShipmentPartiallyDelivered_ShouldAppendEventExactlyOnce()
            {
                var orderId = await SeedPaymentConfirmedOrderAsync(CancellationToken);
                var message = new ShipmentPartiallyDelivered(
                    ShipmentId: 1,
                    OrderId: orderId,
                    DeliveredItems: new List<ShipmentLineItem> { new(ProductId: 1, Quantity: 1) },
                    FailedItems: new List<ShipmentLineItem>(),
                    OccurredAt: DateTime.UtcNow);

                await RedeliverAsync(message, outboxMessageId: 910003, CancellationToken);
                await RedeliverAsync(message, outboxMessageId: 910003, CancellationToken);

                var order = await ReloadOrderAsync(orderId, CancellationToken);
                order.Events.Count(e => e.EventType == OrderEventType.PartiallyFulfilled).ShouldBe(1);
            }
        }

        public class OrderRefundApprovedHandlerDuplicateDeliveryTests : SalesOrdersDuplicateDeliveryTests
        {
            public OrderRefundApprovedHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

            [Fact]
            public async Task RedeliverAsync_SameRefundApproved_ShouldAppendEventExactlyOnce()
            {
                var orderId = await SeedOrderAsync(CancellationToken);
                var message = new RefundApproved(
                    RefundId: 1,
                    OrderId: orderId,
                    Items: new List<RefundApprovedItem> { new(ProductId: 1, Quantity: 1) },
                    OccurredAt: DateTime.UtcNow);

                await RedeliverAsync(message, outboxMessageId: 910004, CancellationToken);
                await RedeliverAsync(message, outboxMessageId: 910004, CancellationToken);

                var order = await ReloadOrderAsync(orderId, CancellationToken);
                order.Events.Count(e => e.EventType == OrderEventType.RefundAssigned).ShouldBe(1);
            }
        }

        public class OrderRefundRejectedHandlerDuplicateDeliveryTests : SalesOrdersDuplicateDeliveryTests
        {
            public OrderRefundRejectedHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

            [Fact]
            public async Task RedeliverAsync_SameRefundRejected_ShouldAppendEventExactlyOnce()
            {
                var orderId = await SeedOrderAsync(CancellationToken);
                // OrderRefundRejectedHandler resolves the order via GetByRefundIdWithItemsAsync, which
                // looks up a RefundAssigned event carrying this RefundId — seed one first via the
                // already-proven RefundApproved path (a different outboxMessageId, a real distinct
                // business event, not part of the redelivery under test).
                var refundApproved = new RefundApproved(
                    RefundId: 42,
                    OrderId: orderId,
                    Items: new List<RefundApprovedItem> { new(ProductId: 1, Quantity: 1) },
                    OccurredAt: DateTime.UtcNow);
                await PublishAsync(refundApproved, CancellationToken);

                var message = new RefundRejected(RefundId: 42, OrderId: orderId, OccurredAt: DateTime.UtcNow);

                await RedeliverAsync(message, outboxMessageId: 910005, CancellationToken);
                await RedeliverAsync(message, outboxMessageId: 910005, CancellationToken);

                var order = await ReloadOrderAsync(orderId, CancellationToken);
                order.Events.Count(e => e.EventType == OrderEventType.RefundRemoved).ShouldBe(1);
            }
        }
    }
}
