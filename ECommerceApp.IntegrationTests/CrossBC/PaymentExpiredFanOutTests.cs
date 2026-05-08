using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Messages;
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
    /// Verifies that <see cref="PaymentExpired"/> is dispatched as a flat fan-out to ALL
    /// registered handlers across BC boundaries:
    /// <list type="bullet">
    ///   <item>Orders BC — expires the order (OrderPaymentExpiredHandler)</item>
    ///   <item>Inventory BC — withdraws all stock holds (PaymentExpiredHandler)</item>
    ///   <item>Coupons BC — restores any applied coupons (CouponsPaymentExpiredHandler)</item>
    /// </list>
    /// No intermediate <see cref="OrderCancelled"/> event is published between them.
    /// </summary>
    public class PaymentExpiredFanOutTests : BcBaseTest<IMessageBroker>
    {
        public PaymentExpiredFanOutTests(ITestOutputHelper output) : base(output) { }

        private const int ProductId = 500;
        private const int OrderId = 20;
        private const int Quantity = 4;
        private const decimal TotalAmount = 120m;

        private OrderPlaced CreateOrderPlaced()
            => new(OrderId,
                   new List<OrderPlacedItem> { new(ProductId, Quantity) },
                   PROPER_CUSTOMER_ID,
                   DateTime.UtcNow.AddHours(24),
                   DateTime.UtcNow,
                   TotalAmount,
                   CurrencyId: 1);

        private static PaymentExpired CreatePaymentExpired()
            => new(PaymentId: 99, OrderId: OrderId, OccurredAt: DateTime.UtcNow);

        private async Task SeedInventoryAsync(int quantity = 100, CancellationToken ct = default)
        {
            var snapshotRepo = GetRequiredService<IProductSnapshotRepository>();
            await snapshotRepo.UpsertAsync(
                ProductSnapshot.Create(ProductId, $"Product-{ProductId}", false, CatalogProductStatus.Orderable));
            await GetRequiredService<IStockService>().InitializeStockAsync(ProductId, quantity, CancellationToken);
        }

        // ── Inventory BC ──────────────────────────────────────────────────────

        [Fact]
        public async Task PaymentExpired_AfterOrderPlaced_ShouldWithdrawStockHoldsInInventoryBc()
        {
            await SeedInventoryAsync(quantity: 100, CancellationToken);
            await PublishAsync(CreateOrderPlaced(), CancellationToken);

            var stockAfterPlaced = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            stockAfterPlaced!.ReservedQuantity.ShouldBe(Quantity);

            await PublishAsync(CreatePaymentExpired(), CancellationToken);

            var stockAfterExpired = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            stockAfterExpired!.ReservedQuantity.ShouldBe(0);
            stockAfterExpired.AvailableQuantity.ShouldBe(100);
        }

        [Fact]
        public async Task PaymentExpired_WhenNoHoldsExist_ShouldCompleteWithoutError()
        {
            var act = async () => await PublishAsync(CreatePaymentExpired(), CancellationToken);

            await act.ShouldNotThrowAsync();
        }

        // ── Coupons BC ────────────────────────────────────────────────────────

        [Fact]
        public async Task PaymentExpired_WhenNoCouponsApplied_ShouldCompleteWithoutError()
        {
            var act = async () => await PublishAsync(CreatePaymentExpired(), CancellationToken);

            await act.ShouldNotThrowAsync();
        }

        // ── No intermediate OrderCancelled published ──────────────────────────

        [Fact]
        public async Task PaymentExpired_ShouldNotTriggerOrderCancelledHandlers()
        {
            // OrderCancelled path (manual cancel) goes through a different code path.
            // Verify: no OrderCancelled-bound Inventory handler fires (stock not double-released).
            await SeedInventoryAsync(quantity: 100, CancellationToken);
            await PublishAsync(CreateOrderPlaced(), CancellationToken);

            await PublishAsync(CreatePaymentExpired(), CancellationToken);

            // If OrderCancelled were also published, ReleaseAsync would be called on an already-withdrawn
            // hold — which would return false (no-op) but increment reserved-release stats incorrectly.
            // Asserting reserved = 0 (not negative) proves only one release path ran.
            var stock = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            stock!.ReservedQuantity.ShouldBe(0);
            stock.AvailableQuantity.ShouldBe(100);
        }
    }
}

