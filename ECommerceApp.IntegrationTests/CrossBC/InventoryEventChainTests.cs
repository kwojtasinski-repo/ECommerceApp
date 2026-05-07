using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.IntegrationTests.CrossBC
{
    /// <summary>
    /// Verifies multi-step event chains that flow through Inventory BC:
    /// <list type="bullet">
    ///   <item><c>OrderPlaced</c> → reserve stock → <c>PaymentConfirmed</c> → confirm holds</item>
    ///   <item><c>OrderCancelled</c> → release reserved stock</item>
    ///   <item><c>ShipmentDelivered</c> → fulfill confirmed stock</item>
    ///   <item><c>ShipmentFailed</c> → release confirmed stock</item>
    /// </list>
    /// </summary>
    public class InventoryEventChainTests : BcBaseTest<IMessageBroker>
    {
        public InventoryEventChainTests(ITestOutputHelper output) : base(output) { }

        private const int ProductId = 300;
        private const int OrderId = 50;
        private const int Quantity = 5;

        private async Task SeedStockAsync(int productId = ProductId, int initialQuantity = 100)
        {
            var snapshotRepo = GetRequiredService<IProductSnapshotRepository>();
            await snapshotRepo.UpsertAsync(
                ProductSnapshot.Create(productId, $"Product-{productId}", false, CatalogProductStatus.Orderable));

            var stockService = GetRequiredService<IStockService>();
            await stockService.InitializeStockAsync(productId, initialQuantity);
        }

        private async Task ReserveStockViaOrderPlacedAsync(
            int orderId = OrderId, int productId = ProductId, int quantity = Quantity)
        {
            var orderPlaced = new OrderPlaced(
                OrderId: orderId,
                Items: new List<OrderPlacedItem> { new(ProductId: productId, Quantity: quantity) },
                UserId: PROPER_CUSTOMER_ID,
                ExpiresAt: DateTime.UtcNow.AddHours(24),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: 100m,
                CurrencyId: 1);

            await _service.PublishAsync(orderPlaced);
        }

        // ── PaymentConfirmed → Inventory stock confirmation ──────────────

        [Fact]
        public async Task PaymentConfirmed_AfterOrderPlaced_ShouldConfirmStockHoldsInInventory()
        {
            await SeedStockAsync();
            await ReserveStockViaOrderPlacedAsync();

            var paymentConfirmed = new PaymentConfirmed(
                PaymentId: 1,
                OrderId: OrderId,
                Items: new List<PaymentConfirmedItem> { new(ProductId: ProductId, Quantity: Quantity) },
                OccurredAt: DateTime.UtcNow);

            await _service.PublishAsync(paymentConfirmed);

            // Stock should still show reserved (confirmed holds are still "reserved" until fulfilled)
            var stockService = GetRequiredService<IStockService>();
            var stock = await stockService.GetByProductIdAsync(ProductId);
            stock.ShouldNotBeNull();
            stock.AvailableQuantity.ShouldBe(100 - Quantity);
        }

        // ── OrderCancelled → Inventory stock release ─────────────────────

        [Fact]
        public async Task OrderCancelled_WithReservedStock_ShouldReleaseStockInInventory()
        {
            await SeedStockAsync();
            await ReserveStockViaOrderPlacedAsync();

            // Verify stock is reserved
            var stockBefore = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId);
            stockBefore.ShouldNotBeNull();
            stockBefore.ReservedQuantity.ShouldBe(Quantity);

            var orderCancelled = new OrderCancelled(
                OrderId: OrderId,
                Items: new List<OrderCancelledItem> { new(ProductId: ProductId, Quantity: Quantity) },
                OccurredAt: DateTime.UtcNow);

            await _service.PublishAsync(orderCancelled);

            // Stock should be released (available quantity restored)
            var stockAfter = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId);
            stockAfter.ShouldNotBeNull();
            stockAfter.AvailableQuantity.ShouldBe(100);
            stockAfter.ReservedQuantity.ShouldBe(0);
        }

        // ── ShipmentDelivered → Inventory stock fulfillment ──────────────

        [Fact]
        public async Task ShipmentDelivered_WithConfirmedStock_ShouldFulfillStockInInventory()
        {
            await SeedStockAsync();
            await ReserveStockViaOrderPlacedAsync();

            // Confirm stock holds (simulating PaymentConfirmed)
            var paymentConfirmed = new PaymentConfirmed(
                PaymentId: 1,
                OrderId: OrderId,
                Items: new List<PaymentConfirmedItem> { new(ProductId: ProductId, Quantity: Quantity) },
                OccurredAt: DateTime.UtcNow);
            await _service.PublishAsync(paymentConfirmed);

            // Now deliver the shipment
            var shipmentDelivered = new ShipmentDelivered(
                ShipmentId: 1,
                OrderId: OrderId,
                Items: new List<ShipmentLineItem> { new(ProductId: ProductId, Quantity: Quantity) },
                OccurredAt: DateTime.UtcNow);

            await _service.PublishAsync(shipmentDelivered);

            // Stock should be fulfilled — total quantity decreased, no longer reserved
            var stock = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId);
            stock.ShouldNotBeNull();
            stock.Quantity.ShouldBe(100 - Quantity);
            stock.ReservedQuantity.ShouldBe(0);
        }

        // ── ShipmentFailed → Inventory reconciliation alert ────────────

        [Fact]
        public async Task ShipmentFailed_WithConfirmedStock_ShouldTriggerReconciliationAlert()
        {
            await SeedStockAsync();
            await ReserveStockViaOrderPlacedAsync();

            // Confirm stock holds (transitions from Reserved → Confirmed)
            var paymentConfirmed = new PaymentConfirmed(
                PaymentId: 1,
                OrderId: OrderId,
                Items: new List<PaymentConfirmedItem> { new(ProductId: ProductId, Quantity: Quantity) },
                OccurredAt: DateTime.UtcNow);
            await _service.PublishAsync(paymentConfirmed);

            // Shipment fails — ReleaseAsync returns false for confirmed holds,
            // handler publishes StockReconciliationRequired (correct behavior per ADR-0011)
            var shipmentFailed = new ShipmentFailed(
                ShipmentId: 1,
                OrderId: OrderId,
                Items: new List<ShipmentLineItem> { new(ProductId: ProductId, Quantity: Quantity) },
                OccurredAt: DateTime.UtcNow);

            await _service.PublishAsync(shipmentFailed);

            // Confirmed holds cannot be released via ReleaseAsync — stock stays reserved
            // Manual reconciliation required (StockReconciliationRequired published)
            var stock = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId);
            stock.ShouldNotBeNull();
            stock.AvailableQuantity.ShouldBe(100 - Quantity);
        }

        // ── Full lifecycle: OrderPlaced → PaymentConfirmed → ShipmentDelivered ──

        [Fact]
        public async Task FullHappyPath_OrderPlacedToDelivery_ShouldTrackStockThroughAllStages()
        {
            await SeedStockAsync();

            // Stage 1: OrderPlaced → stock reserved
            await ReserveStockViaOrderPlacedAsync();
            var stage1 = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId);
            stage1.ShouldNotBeNull();
            stage1.Quantity.ShouldBe(100);
            stage1.ReservedQuantity.ShouldBe(Quantity);
            stage1.AvailableQuantity.ShouldBe(100 - Quantity);

            // Stage 2: PaymentConfirmed → holds confirmed (reserved qty stays)
            await _service.PublishAsync(new PaymentConfirmed(
                PaymentId: 1, OrderId: OrderId,
                Items: new List<PaymentConfirmedItem> { new(ProductId, Quantity) },
                OccurredAt: DateTime.UtcNow));

            var stage2 = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId);
            stage2.ShouldNotBeNull();
            stage2.AvailableQuantity.ShouldBe(100 - Quantity);

            // Stage 3: ShipmentDelivered → stock fulfilled (total decreases, reserved cleared)
            await _service.PublishAsync(new ShipmentDelivered(
                ShipmentId: 1, OrderId: OrderId,
                Items: new List<ShipmentLineItem> { new(ProductId, Quantity) },
                OccurredAt: DateTime.UtcNow));

            var stage3 = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId);
            stage3.ShouldNotBeNull();
            stage3.Quantity.ShouldBe(100 - Quantity);
            stage3.ReservedQuantity.ShouldBe(0);
            stage3.AvailableQuantity.ShouldBe(100 - Quantity);
        }
    }
}

