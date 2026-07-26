using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Domain.Sales.Payments;
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
    /// Verifies that <see cref="OrderPlacementFailed"/> event dispatches to ALL registered
    /// compensation handlers across BC boundaries via <see cref="SynchronousMultiHandlerBroker"/>:
    /// <list type="bullet">
    ///   <item>Payments BC — cancels the pending payment</item>
    ///   <item>Inventory BC — releases stock holds per order item</item>
    ///   <item>Presale BC — restores the user's cart</item>
    /// </list>
    /// </summary>
    public class OrderPlacementFailedFanOutTests : BcBaseTest<IMessageBroker>
    {
        public OrderPlacementFailedFanOutTests(ITestOutputHelper output) : base(output) { }

        private const int ProductId = 300;
        private const int OrderId = 10;
        private const int Quantity = 3;
        private const decimal TotalAmount = 75m;
        private const string CartUserId = "user-1";

        private OrderPlaced CreateOrderPlaced(int orderId = OrderId, int productId = ProductId, int quantity = Quantity, string userId = null)
            => new(orderId,
                   new List<OrderPlacedItem> { new(productId, quantity) },
                   userId ?? PROPER_CUSTOMER_ID,
                   DateTime.UtcNow.AddHours(24),
                   DateTime.UtcNow,
                   TotalAmount,
                   CurrencyId: 1);

        private static OrderPlacementFailed CreateOrderPlacementFailed(int orderId = OrderId, int productId = ProductId, int quantity = Quantity)
            => new(orderId,
                   "inventory handler threw",
                   new List<OrderPlacedItem> { new(productId, quantity) },
                   UserId: CartUserId);

        private async Task SeedInventoryAsync(int productId = ProductId, int initialQuantity = 100, CancellationToken ct = default)
        {
            var snapshotRepo = GetRequiredService<IProductSnapshotRepository>();
            await snapshotRepo.UpsertAsync(
                ProductSnapshot.Create(productId, $"Product-{productId}", false, CatalogProductStatus.Orderable));

            var stockService = GetRequiredService<IStockService>();
            await stockService.InitializeStockAsync(productId, initialQuantity, CancellationToken);
        }

        // ── Payments BC compensation ──────────────────────────────────────────

        [Fact]
        public async Task OrderPlacementFailed_AfterOrderPlaced_ShouldCancelPaymentInPaymentsBc()
        {
            await PublishAsync(CreateOrderPlaced(), CancellationToken);

            await PublishAsync(CreateOrderPlacementFailed(), CancellationToken);

            var paymentService = GetRequiredService<IPaymentService>();
            var payment = await paymentService.GetByOrderIdAsync(OrderId, CancellationToken);
            payment.ShouldNotBeNull();
            payment.Status.ShouldBe(PaymentStatus.Cancelled.ToString());
        }

        [Fact]
        public async Task OrderPlacementFailed_WhenPaymentNotYetCreated_ShouldCompleteWithoutError()
        {
            var act = async () => await PublishAsync(CreateOrderPlacementFailed(), CancellationToken);

            await act.ShouldNotThrowAsync();
        }

        // ── Inventory BC compensation ─────────────────────────────────────────

        [Fact]
        public async Task OrderPlacementFailed_AfterOrderPlaced_ShouldReleaseStockHoldsInInventoryBc()
        {
            await SeedInventoryAsync(initialQuantity: 100, ct: CancellationToken);
            await PublishAsync(CreateOrderPlaced(), CancellationToken);

            var stockAfterPlaced = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            stockAfterPlaced!.ReservedQuantity.ShouldBe(Quantity);

            await PublishAsync(CreateOrderPlacementFailed(), CancellationToken);

            // Resolve a fresh service to avoid stale EF change-tracker returning cached pre-release values.
            var stockAfterFailed = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            stockAfterFailed!.ReservedQuantity.ShouldBe(0);
            stockAfterFailed.AvailableQuantity.ShouldBe(100);
        }

        [Fact]
        public async Task OrderPlacementFailed_WhenNoStockHoldsExist_ShouldCompleteWithoutError()
        {
            var act = async () => await PublishAsync(CreateOrderPlacementFailed(), CancellationToken);

            await act.ShouldNotThrowAsync();
        }

        // ── Presale BC compensation ─────────────────────────────────────────

        [Fact]
        public async Task OrderPlacementFailed_AfterOrderPlaced_ShouldRestoreCartInPresaleBc()
        {
            var cartService = GetRequiredService<ICartService>();
            await cartService.SetCartItemAsync(new AddToCartDto(CartUserId, ProductId, Quantity), CancellationToken);

            await PublishAsync(CreateOrderPlaced(userId: CartUserId), CancellationToken);

            var afterPlaced = await cartService.GetCartAsync(new PresaleUserId(CartUserId), CancellationToken);
            afterPlaced.ShouldBeNull();

            await PublishAsync(CreateOrderPlacementFailed(), CancellationToken);

            var afterFailed = await cartService.GetCartAsync(new PresaleUserId(CartUserId), CancellationToken);
            afterFailed.ShouldNotBeNull();
            afterFailed.Lines.Count.ShouldBe(1);
            afterFailed.Lines[0].ProductId.ShouldBe(ProductId);
            afterFailed.Lines[0].Quantity.ShouldBe(Quantity);
        }

        // ── Cross-BC fan-out ──────────────────────────────────────────────────

        [Fact]
        public async Task OrderPlacementFailed_ShouldCompensateBothPaymentsAndInventory()
        {
            await SeedInventoryAsync(initialQuantity: 100, ct: CancellationToken);
            await PublishAsync(CreateOrderPlaced(), CancellationToken);

            await PublishAsync(CreateOrderPlacementFailed(), CancellationToken);

            var paymentService = GetRequiredService<IPaymentService>();
            var payment = await paymentService.GetByOrderIdAsync(OrderId, CancellationToken);
            payment.ShouldNotBeNull();
            payment.Status.ShouldBe(PaymentStatus.Cancelled.ToString());

            var stockService = GetRequiredService<IStockService>();
            var stock = await stockService.GetByProductIdAsync(ProductId, CancellationToken);
            stock.ShouldNotBeNull();
            stock.ReservedQuantity.ShouldBe(0);
            stock.AvailableQuantity.ShouldBe(100);
        }
    }
}

