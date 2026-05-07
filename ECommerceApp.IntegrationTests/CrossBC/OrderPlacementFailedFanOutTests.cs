using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Sales.Payments;
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
    /// Verifies that <see cref="OrderPlacementFailed"/> event dispatches to ALL registered
    /// compensation handlers across BC boundaries via <see cref="SynchronousMultiHandlerBroker"/>:
    /// <list type="bullet">
    ///   <item>Payments BC — cancels the pending payment</item>
    ///   <item>Inventory BC — releases stock holds per order item</item>
    ///   <item>Presale BC — logs warning (cart restore deferred)</item>
    /// </list>
    /// </summary>
    public class OrderPlacementFailedFanOutTests : BcBaseTest<IMessageBroker>
    {
        public OrderPlacementFailedFanOutTests(ITestOutputHelper output) : base(output) { }

        private const int ProductId = 300;
        private const int OrderId = 10;
        private const int Quantity = 3;
        private const decimal TotalAmount = 75m;

        private OrderPlaced CreateOrderPlaced(int orderId = OrderId, int productId = ProductId, int quantity = Quantity)
            => new(orderId,
                   new List<OrderPlacedItem> { new(productId, quantity) },
                   PROPER_CUSTOMER_ID,
                   DateTime.UtcNow.AddHours(24),
                   DateTime.UtcNow,
                   TotalAmount,
                   CurrencyId: 1);

        private static OrderPlacementFailed CreateOrderPlacementFailed(int orderId = OrderId, int productId = ProductId, int quantity = Quantity)
            => new(orderId,
                   "inventory handler threw",
                   new List<OrderPlacedItem> { new(productId, quantity) },
                   UserId: "user-1");

        private async Task SeedInventoryAsync(int productId = ProductId, int initialQuantity = 100)
        {
            var snapshotRepo = GetRequiredService<IProductSnapshotRepository>();
            await snapshotRepo.UpsertAsync(
                ProductSnapshot.Create(productId, $"Product-{productId}", false, CatalogProductStatus.Orderable));

            var stockService = GetRequiredService<IStockService>();
            await stockService.InitializeStockAsync(productId, initialQuantity);
        }

        // ── Payments BC compensation ──────────────────────────────────────────

        [Fact]
        public async Task OrderPlacementFailed_AfterOrderPlaced_ShouldCancelPaymentInPaymentsBc()
        {
            await _service.PublishAsync(CreateOrderPlaced());

            await _service.PublishAsync(CreateOrderPlacementFailed());

            var paymentService = GetRequiredService<IPaymentService>();
            var payment = await paymentService.GetByOrderIdAsync(OrderId);
            payment.ShouldNotBeNull();
            payment.Status.ShouldBe(PaymentStatus.Cancelled.ToString());
        }

        [Fact]
        public async Task OrderPlacementFailed_WhenPaymentNotYetCreated_ShouldCompleteWithoutError()
        {
            var act = async () => await _service.PublishAsync(CreateOrderPlacementFailed());

            await act.ShouldNotThrowAsync();
        }

        // ── Inventory BC compensation ─────────────────────────────────────────

        [Fact]
        public async Task OrderPlacementFailed_AfterOrderPlaced_ShouldReleaseStockHoldsInInventoryBc()
        {
            await SeedInventoryAsync(initialQuantity: 100);
            await _service.PublishAsync(CreateOrderPlaced());

            var stockAfterPlaced = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId);
            stockAfterPlaced!.ReservedQuantity.ShouldBe(Quantity);

            await _service.PublishAsync(CreateOrderPlacementFailed());

            // Resolve a fresh service to avoid stale EF change-tracker returning cached pre-release values.
            var stockAfterFailed = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId);
            stockAfterFailed!.ReservedQuantity.ShouldBe(0);
            stockAfterFailed.AvailableQuantity.ShouldBe(100);
        }

        [Fact]
        public async Task OrderPlacementFailed_WhenNoStockHoldsExist_ShouldCompleteWithoutError()
        {
            var act = async () => await _service.PublishAsync(CreateOrderPlacementFailed());

            await act.ShouldNotThrowAsync();
        }

        // ── Cross-BC fan-out ──────────────────────────────────────────────────

        [Fact]
        public async Task OrderPlacementFailed_ShouldCompensateBothPaymentsAndInventory()
        {
            await SeedInventoryAsync(initialQuantity: 100);
            await _service.PublishAsync(CreateOrderPlaced());

            await _service.PublishAsync(CreateOrderPlacementFailed());

            var paymentService = GetRequiredService<IPaymentService>();
            var payment = await paymentService.GetByOrderIdAsync(OrderId);
            payment.ShouldNotBeNull();
            payment.Status.ShouldBe(PaymentStatus.Cancelled.ToString());

            var stockService = GetRequiredService<IStockService>();
            var stock = await stockService.GetByProductIdAsync(ProductId);
            stock.ShouldNotBeNull();
            stock.ReservedQuantity.ShouldBe(0);
            stock.AvailableQuantity.ShouldBe(100);
        }
    }
}

