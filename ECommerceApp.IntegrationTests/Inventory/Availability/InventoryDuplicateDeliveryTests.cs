using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Inventory.Availability
{
    /// <summary>
    /// Phase 4 Inbox-idempotency proof for the 5 Inventory handlers audited "needs dedup" in
    /// <c>.github/plans/04-phase-inbox-idempotency-implementation.md</c>. Each test delivers the same
    /// Outbox row twice (<see cref="BcBaseTest{T}.RedeliverAsync"/> with a shared explicit id) and
    /// asserts the stock-quantity side effect changed exactly once.
    /// <para>
    /// Base type is deliberately <see cref="IMessageBroker"/>, not <see cref="IStockService"/> — resolving
    /// a DbContext-backed service once via <c>BcBaseTest&lt;T&gt;</c>'s eagerly-cached <c>_service</c>
    /// field and then reading through that SAME cached instance after a redelivery mutated the store via a
    /// different (broker-resolved) instance returns EF's stale tracked entity, not the fresh row — the
    /// exact gotcha <c>PaymentServiceOutboxIntegrationTests.GetOrderStatusAsync</c> already documents.
    /// Every stock read/write below goes through a freshly-resolved <see cref="IStockService"/> instead.
    /// </para>
    /// <para>
    /// The 3 <c>FulfillAsync</c>-based handlers (ShipmentDelivered/ShipmentPartiallyDelivered/OrderShipped)
    /// seed a <b>second, unrelated order's hold on the same product</b> ("decoy") before redelivering —
    /// without that decoy, <c>StockService.FulfillAsync</c>'s own aggregate-<c>ReservedQuantity</c> guard
    /// would coincidentally drop to 0 after the first fulfill and block a naive redelivery on its own,
    /// which would prove nothing about whether the Inbox guard is actually doing the work. With the decoy
    /// hold providing spare aggregate headroom, a redelivery that reached <c>FulfillAsync</c> a second
    /// time would pass its guard and wrongly consume the decoy order's reservation — exactly the
    /// concurrent-order scenario the plan's audit flagged as the aggregate-guard's real weakness. This
    /// proves the Inbox guard specifically, not an accidental side effect of the domain guard.
    /// </para>
    /// </summary>
    public class InventoryDuplicateDeliveryTests : BcBaseTest<IMessageBroker>
    {
        public InventoryDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

        private async Task SeedStockAsync(int productId, int initialQuantity, CancellationToken ct = default)
        {
            var snapshotRepo = GetRequiredService<IProductSnapshotRepository>();
            await snapshotRepo.UpsertAsync(
                ProductSnapshot.Create(productId, $"Product-{productId}", false, CatalogProductStatus.Orderable));

            await GetRequiredService<IStockService>().InitializeStockAsync(productId, initialQuantity, ct);
        }

        private async Task ReserveViaOrderPlacedAsync(int orderId, int productId, int quantity, CancellationToken ct = default)
        {
            var orderPlaced = new OrderPlaced(
                OrderId: orderId,
                Items: new List<OrderPlacedItem> { new(ProductId: productId, Quantity: quantity) },
                UserId: PROPER_CUSTOMER_ID,
                ExpiresAt: DateTime.UtcNow.AddHours(24),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: 100m,
                CurrencyId: 1);

            await PublishAsync(orderPlaced, ct);
        }

        private Task<StockItemDto> GetStockAsync(int productId, CancellationToken ct = default)
            => GetRequiredService<IStockService>().GetByProductIdAsync(productId, ct);

        public class ShipmentDeliveredHandlerDuplicateDeliveryTests : InventoryDuplicateDeliveryTests
        {
            public ShipmentDeliveredHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

            [Fact]
            public async Task RedeliverAsync_SameShipmentDelivered_ShouldFulfillStockExactlyOnce()
            {
                const int productId = 90001;
                const int orderId = 1;
                const int decoyOrderId = 2;

                await SeedStockAsync(productId, initialQuantity: 100, CancellationToken);
                await ReserveViaOrderPlacedAsync(orderId, productId, quantity: 10, CancellationToken);
                await ReserveViaOrderPlacedAsync(decoyOrderId, productId, quantity: 10, CancellationToken);

                var message = new ShipmentDelivered(
                    ShipmentId: 1,
                    OrderId: orderId,
                    Items: new List<ShipmentLineItem> { new(ProductId: productId, Quantity: 10) },
                    OccurredAt: DateTime.UtcNow);

                await RedeliverAsync(message, outboxMessageId: 900001, CancellationToken);
                await RedeliverAsync(message, outboxMessageId: 900001, CancellationToken);

                var stock = await GetStockAsync(productId, CancellationToken);
                stock.ShouldNotBeNull();
                stock!.Quantity.ShouldBe(90);
                stock.ReservedQuantity.ShouldBe(10);
            }
        }

        public class ShipmentPartiallyDeliveredHandlerDuplicateDeliveryTests : InventoryDuplicateDeliveryTests
        {
            public ShipmentPartiallyDeliveredHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

            [Fact]
            public async Task RedeliverAsync_SameShipmentPartiallyDelivered_ShouldFulfillStockExactlyOnce()
            {
                const int productId = 90002;
                const int orderId = 1;
                const int decoyOrderId = 2;

                await SeedStockAsync(productId, initialQuantity: 100, CancellationToken);
                await ReserveViaOrderPlacedAsync(orderId, productId, quantity: 10, CancellationToken);
                await ReserveViaOrderPlacedAsync(decoyOrderId, productId, quantity: 10, CancellationToken);

                var message = new ShipmentPartiallyDelivered(
                    ShipmentId: 1,
                    OrderId: orderId,
                    DeliveredItems: new List<ShipmentLineItem> { new(ProductId: productId, Quantity: 10) },
                    FailedItems: new List<ShipmentLineItem>(),
                    OccurredAt: DateTime.UtcNow);

                await RedeliverAsync(message, outboxMessageId: 900002, CancellationToken);
                await RedeliverAsync(message, outboxMessageId: 900002, CancellationToken);

                var stock = await GetStockAsync(productId, CancellationToken);
                stock.ShouldNotBeNull();
                stock!.Quantity.ShouldBe(90);
                stock.ReservedQuantity.ShouldBe(10);
            }
        }

        // NOTE: Inventory.Availability.Handlers.OrderShippedHandler has no DuplicateDeliveryTests here —
        // it is NOT registered for IMessageHandler<OrderShipped> in
        // ECommerceApp.Application/Inventory/Availability/Services/Extensions.cs ("OrderShippedHandler
        // unregistered — replaced by Fulfillment handlers (ADR-0017 §13.3)"). It is dead/unreachable code;
        // a broker-dispatched redelivery test would prove nothing, since PublishAsync/RedeliverAsync never
        // finds this handler registered at all. The audit table's "needs dedup" classification and the
        // dedup wiring already applied to this handler are both harmless but moot — see the plan file's
        // correction note. Coverage for it is the existing mocked ECommerceApp.UnitTests/Inventory/
        // Availability/OrderShippedHandlerTests.cs, which exercises the class directly.

        public class OrderPlacedHandlerDuplicateDeliveryTests : InventoryDuplicateDeliveryTests
        {
            public OrderPlacedHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

            [Fact]
            public async Task RedeliverAsync_SameOrderPlaced_ShouldReserveStockAndCreateHoldExactlyOnce()
            {
                const int productId = 90004;
                const int orderId = 1;

                await SeedStockAsync(productId, initialQuantity: 100, CancellationToken);

                var message = new OrderPlaced(
                    OrderId: orderId,
                    Items: new List<OrderPlacedItem> { new(ProductId: productId, Quantity: 10) },
                    UserId: PROPER_CUSTOMER_ID,
                    ExpiresAt: DateTime.UtcNow.AddHours(24),
                    OccurredAt: DateTime.UtcNow,
                    TotalAmount: 100m,
                    CurrencyId: 1);

                await RedeliverAsync(message, outboxMessageId: 900004, CancellationToken);
                await RedeliverAsync(message, outboxMessageId: 900004, CancellationToken);

                var stock = await GetStockAsync(productId, CancellationToken);
                stock.ShouldNotBeNull();
                stock!.ReservedQuantity.ShouldBe(10);

                var holds = await GetRequiredService<IStockHoldRepository>().GetByOrderIdAsync(orderId, CancellationToken);
                holds.Count.ShouldBe(1);
            }
        }

        public class RefundApprovedHandlerDuplicateDeliveryTests : InventoryDuplicateDeliveryTests
        {
            public RefundApprovedHandlerDuplicateDeliveryTests(ITestOutputHelper output) : base(output) { }

            [Fact]
            public async Task RedeliverAsync_SameRefundApproved_ShouldReturnStockExactlyOnce()
            {
                const int productId = 90005;

                await SeedStockAsync(productId, initialQuantity: 100, CancellationToken);

                var message = new RefundApproved(
                    RefundId: 1,
                    OrderId: 1,
                    Items: new List<RefundApprovedItem> { new(ProductId: productId, Quantity: 10) },
                    OccurredAt: DateTime.UtcNow);

                await RedeliverAsync(message, outboxMessageId: 900005, CancellationToken);
                await RedeliverAsync(message, outboxMessageId: 900005, CancellationToken);

                var stock = await GetStockAsync(productId, CancellationToken);
                stock.ShouldNotBeNull();
                stock!.Quantity.ShouldBe(110);
            }
        }
    }
}
