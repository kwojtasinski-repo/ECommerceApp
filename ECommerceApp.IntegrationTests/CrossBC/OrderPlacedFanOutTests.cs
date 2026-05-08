using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.CrossBC
{
    /// <summary>
    /// Verifies that <see cref="OrderPlaced"/> event dispatches to ALL registered handlers
    /// across BC boundaries via <see cref="SynchronousMultiHandlerBroker"/>:
    /// <list type="bullet">
    ///   <item>Payments BC — creates a <c>Payment</c> (Pending)</item>
    ///   <item>Inventory BC — reserves stock per order item</item>
    ///   <item>Presale BC — clears cart + soft reservations (no-op when empty)</item>
    ///   <item>Orders BC — snapshot handler (no-op when no order items exist)</item>
    /// </list>
    /// </summary>
    public class OrderPlacedFanOutTests : BcBaseTest<IMessageBroker>
    {
        public OrderPlacedFanOutTests(ITestOutputHelper output) : base(output) { }

        private const int ProductId = 200;
        private const int OrderId = 1;
        private const int Quantity = 2;
        private const decimal TotalAmount = 50m;

        private OrderPlaced CreateOrderPlaced(int orderId = OrderId, int productId = ProductId, int quantity = Quantity)
        {
            return new OrderPlaced(
                OrderId: orderId,
                Items: new List<OrderPlacedItem> { new(ProductId: productId, Quantity: quantity) },
                UserId: PROPER_CUSTOMER_ID,
                ExpiresAt: DateTime.UtcNow.AddHours(24),
                OccurredAt: DateTime.UtcNow,
                TotalAmount: TotalAmount,
                CurrencyId: 1);
        }

        private async Task SeedInventoryAsync(int productId = ProductId, int initialQuantity = 100, CancellationToken ct = default)
        {
            var snapshotRepo = GetRequiredService<IProductSnapshotRepository>();
            await snapshotRepo.UpsertAsync(
                ProductSnapshot.Create(productId, $"Product-{productId}", false, CatalogProductStatus.Orderable));

            var stockService = GetRequiredService<IStockService>();
            await stockService.InitializeStockAsync(productId, initialQuantity, CancellationToken);
        }

        // ── Payments BC ──────────────────────────────────────────────────

        [Fact]
        public async Task OrderPlaced_ShouldCreatePaymentInPaymentsBc()
        {
            var orderPlaced = CreateOrderPlaced();

            await PublishAsync(orderPlaced, CancellationToken);

            var paymentService = GetRequiredService<IPaymentService>();
            var payment = await paymentService.GetByOrderIdAsync(OrderId, CancellationToken);
            payment.ShouldNotBeNull();
            payment.OrderId.ShouldBe(OrderId);
        }

        // ── Inventory BC ─────────────────────────────────────────────────

        [Fact]
        public async Task OrderPlaced_WithAvailableStock_ShouldReserveStockInInventoryBc()
        {
            await SeedInventoryAsync(ct: CancellationToken);
            var orderPlaced = CreateOrderPlaced();

            await PublishAsync(orderPlaced, CancellationToken);

            var stockService = GetRequiredService<IStockService>();
            var stock = await stockService.GetByProductIdAsync(ProductId, CancellationToken);
            stock.ShouldNotBeNull();
            stock.AvailableQuantity.ShouldBe(100 - Quantity);
            stock.ReservedQuantity.ShouldBe(Quantity);
        }

        // ── Multi-BC fan-out ─────────────────────────────────────────────

        [Fact]
        public async Task OrderPlaced_ShouldFanOutToPaymentsAndInventorySimultaneously()
        {
            await SeedInventoryAsync(ct: CancellationToken);
            var orderPlaced = CreateOrderPlaced();

            await PublishAsync(orderPlaced, CancellationToken);

            // Payments BC: payment should exist
            var paymentService = GetRequiredService<IPaymentService>();
            var payment = await paymentService.GetByOrderIdAsync(OrderId, CancellationToken);
            payment.ShouldNotBeNull();

            // Inventory BC: stock should be reserved
            var stockService = GetRequiredService<IStockService>();
            var stock = await stockService.GetByProductIdAsync(ProductId, CancellationToken);
            stock.ShouldNotBeNull();
            stock.ReservedQuantity.ShouldBe(Quantity);
        }
    }
}

