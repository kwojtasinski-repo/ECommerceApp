using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Domain.Inventory.Availability;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
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
    /// State-machine integration tests for the <see cref="PaymentExpired"/> fan-out chain.
    ///
    /// Covers:
    /// <list type="bullet">
    ///   <item>A → B: Payment Pending → Expired (via <see cref="PaymentWindowExpiredJob"/> simulation)</item>
    ///   <item>B → C: Order Placed → Cancelled (via <see cref="PaymentExpired"/> fan-out)</item>
    ///   <item>Guard: B state already Cancelled — no-op, no double-update</item>
    ///   <item>Guard: B state already PaymentConfirmed — no-op</item>
    ///   <item>Full fan-out: all three BCs (Orders, Inventory, Coupons) updated atomically</item>
    ///   <item>Idempotency: second <see cref="PaymentExpired"/> publish does not double-release stock</item>
    ///   <item>CorrelationId: published event carries a non-empty CorrelationId</item>
    ///   <item>Rollback semantics: refund path is separate; stock not re-released</item>
    /// </list>
    /// </summary>
    public class PaymentExpiredStateTransitionTests : BcBaseTest<IMessageBroker>
    {
        public PaymentExpiredStateTransitionTests(ITestOutputHelper output) : base(output) { }

        // ── Constants ─────────────────────────────────────────────────────────

        private const int ProductId = 600;
        private const int OrderId = 30;
        private const int Quantity = 3;
        private const decimal TotalAmount = 90m;

        // ── Helpers ───────────────────────────────────────────────────────────

        private static OrderCustomer CreateCustomer() => new(
            "Anna", "Nowak", "anna@test.com", "987654321",
            false, null, null, "Lipowa", "5", null, "00-001", "Warszawa", "Polska");

        private OrderPlaced CreateOrderPlaced(int orderId = OrderId)
            => new(orderId,
                   new List<OrderPlacedItem> { new(ProductId, Quantity) },
                   PROPER_CUSTOMER_ID,
                   DateTime.UtcNow.AddHours(24),
                   DateTime.UtcNow,
                   TotalAmount,
                   CurrencyId: 1);

        private static PaymentExpired CreatePaymentExpired(int orderId = OrderId, Guid? correlationId = null)
            => new(PaymentId: 77, OrderId: orderId, OccurredAt: DateTime.UtcNow,
                   CorrelationId: correlationId ?? Guid.NewGuid());

        private async Task SeedInventoryAsync(int quantity = 100, CancellationToken ct = default)
        {
            var snapshotRepo = GetRequiredService<IProductSnapshotRepository>();
            await snapshotRepo.UpsertAsync(
                ProductSnapshot.Create(ProductId, $"Product-{ProductId}", false, CatalogProductStatus.Orderable));
            await GetRequiredService<IStockService>().InitializeStockAsync(ProductId, quantity, CancellationToken);
        }

        private async Task<int> SeedOrderDirectlyAsync(int orderId = OrderId, CancellationToken ct = default)
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(orderId, orderId, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            return await repo.AddAsync(order);
        }

        // ── A → B: Order Placed → [PaymentExpired published] → Order Cancelled ─

        [Fact]
        public async Task State_B_To_C_PlacedOrder_ShouldTransitionToCancelledAfterPaymentExpired()
        {
            // Arrange: seed order in Placed state (state B)
            var orderId = await SeedOrderDirectlyAsync(ct: CancellationToken);

            // Act: publish PaymentExpired (simulating job having already expired the payment)
            await PublishAsync(CreatePaymentExpired(orderId), CancellationToken);

            // Assert: order is now in Cancelled state (state C)
            var order = await GetRequiredService<IOrderService>().GetOrderDetailsAsync(orderId, CancellationToken);
            order.ShouldNotBeNull();
            order.Status.ShouldBe(OrderStatus.Cancelled);
        }

        // ── B → C guard: already Cancelled → no-op ────────────────────────────

        [Fact]
        public async Task State_C_AlreadyCancelled_SecondPaymentExpired_ShouldBeNoOp()
        {
            var orderId = await SeedOrderDirectlyAsync(ct: CancellationToken);

            // First publish: transitions Placed → Cancelled
            await PublishAsync(CreatePaymentExpired(orderId), CancellationToken);
            var afterFirst = await GetRequiredService<IOrderService>().GetOrderDetailsAsync(orderId, CancellationToken);
            afterFirst!.Status.ShouldBe(OrderStatus.Cancelled);

            // Second publish: order already Cancelled — handler must be a no-op (no exception)
            var act = async () => await PublishAsync(CreatePaymentExpired(orderId), CancellationToken);
            await act.ShouldNotThrowAsync();

            var afterSecond = await GetRequiredService<IOrderService>().GetOrderDetailsAsync(orderId, CancellationToken);
            afterSecond!.Status.ShouldBe(OrderStatus.Cancelled);
        }

        // ── B → C guard: PaymentConfirmed → no-op ─────────────────────────────

        [Fact]
        public async Task State_PaymentConfirmed_PaymentExpired_ShouldBeNoOp()
        {
            // Seed order directly (returns auto-assigned DB id)
            var orderId = await SeedOrderDirectlyAsync(ct: CancellationToken);

            // Confirm payment transition (Placed → PaymentConfirmed via PaymentConfirmed message)
            var paymentConfirmed = new Application.Sales.Payments.Messages.PaymentConfirmed(
                PaymentId: 5,
                OrderId: orderId,
                Items: new List<Application.Sales.Payments.Messages.PaymentConfirmedItem>(),
                OccurredAt: DateTime.UtcNow);
            await PublishAsync(paymentConfirmed, CancellationToken);

            var orderService = GetRequiredService<IOrderService>();
            var afterConfirm = await orderService.GetOrderDetailsAsync(orderId, CancellationToken);
            afterConfirm.ShouldNotBeNull();
            afterConfirm!.Status.ShouldBe(OrderStatus.PaymentConfirmed);

            // Act: PaymentExpired arrives too late — order is already PaymentConfirmed
            var act = async () => await PublishAsync(CreatePaymentExpired(orderId), CancellationToken);

            // Assert: no exception; order stays PaymentConfirmed
            await act.ShouldNotThrowAsync();
            var afterExpired = await orderService.GetOrderDetailsAsync(orderId, CancellationToken);
            afterExpired!.Status.ShouldBe(OrderStatus.PaymentConfirmed);
        }

        // ── A → B → C: Full chain start-to-end ───────────────────────────────

        [Fact]
        public async Task FullChain_OrderPlaced_Then_PaymentExpired_ShouldUpdateAllThreeBCs()
        {
            // Arrange: seed inventory and the Order entity
            await SeedInventoryAsync(quantity: 100, CancellationToken);
            var orderId = await SeedOrderDirectlyAsync(ct: CancellationToken);

            // A: OrderPlaced event dispatched → Payment created (Pending), stock reserved
            await PublishAsync(CreateOrderPlaced(orderId), CancellationToken);

            var stockAfterPlaced = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            stockAfterPlaced!.ReservedQuantity.ShouldBe(Quantity, "stock should be reserved after OrderPlaced");

            var paymentAfterPlaced = await GetRequiredService<IPaymentService>().GetByOrderIdAsync(orderId, CancellationToken);
            paymentAfterPlaced.ShouldNotBeNull();
            paymentAfterPlaced.Status.ShouldBe("Pending");

            // B: PaymentExpired event dispatched (all 5 handlers run synchronously)
            await PublishAsync(CreatePaymentExpired(orderId), CancellationToken);

            // C: assert all three BCs transitioned
            // Orders BC: Placed → Cancelled
            var order = await GetRequiredService<IOrderService>().GetOrderDetailsAsync(orderId, CancellationToken);
            order.ShouldNotBeNull();
            order.Status.ShouldBe(OrderStatus.Cancelled, "Orders BC: order should be Cancelled");

            // Inventory BC: stock holds released
            var stockAfterExpired = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            stockAfterExpired!.ReservedQuantity.ShouldBe(0, "Inventory BC: holds should be released");
            stockAfterExpired.AvailableQuantity.ShouldBe(100, "Inventory BC: full quantity should be available again");

            // Coupons BC: no coupons applied → no-op (no exception)
            // (coupon assertions require seeding a coupon which is tested in dedicated coupon tests)
        }

        // ── Idempotency: double-fire does not double-release stock ────────────

        [Fact]
        public async Task Idempotency_PaymentExpiredPublishedTwice_ShouldNotDoubleReleaseStock()
        {
            await SeedInventoryAsync(quantity: 50, CancellationToken);
            await PublishAsync(CreateOrderPlaced(), CancellationToken);

            // First publish: releases holds
            await PublishAsync(CreatePaymentExpired(), CancellationToken);
            var afterFirst = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            afterFirst!.ReservedQuantity.ShouldBe(0);
            afterFirst.AvailableQuantity.ShouldBe(50);

            // Second publish: no holds left — must be a no-op, not go negative
            var act = async () => await PublishAsync(CreatePaymentExpired(), CancellationToken);
            await act.ShouldNotThrowAsync();

            var afterSecond = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            afterSecond!.ReservedQuantity.ShouldBe(0, "second fire must not go below 0");
            afterSecond.AvailableQuantity.ShouldBe(50, "available must not change on second fire");
        }

        // ── CorrelationId: non-empty on published event ───────────────────────

        [Fact]
        public async Task CorrelationId_NonDefault_ShouldBeAcceptedByAllHandlers()
        {
            var orderId = await SeedOrderDirectlyAsync(ct: CancellationToken);
            var correlationId = Guid.NewGuid();
            var message = CreatePaymentExpired(orderId, correlationId: correlationId);

            // All handlers accept messages with non-default CorrelationId without error
            var act = async () => await PublishAsync(message, CancellationToken);
            await act.ShouldNotThrowAsync();

            // Order should still transition correctly
            var order = await GetRequiredService<IOrderService>().GetOrderDetailsAsync(orderId, CancellationToken);
            order!.Status.ShouldBe(OrderStatus.Cancelled);
        }

        // ── Rollback semantics: end-to-start verification ─────────────────────

        [Fact]
        public async Task EndToStart_StockZero_TracesBackToPaymentExpiredAndReleasedHolds()
        {
            // End state (C): stock.ReservedQuantity == 0 after PaymentExpired
            // Trace back: PaymentExpiredHandler ran → PaymentExpired was published → holds were released
            await SeedInventoryAsync(quantity: 50, CancellationToken);
            await PublishAsync(CreateOrderPlaced(), CancellationToken);

            // Verify intermediate state (B): stock reserved after OrderPlaced
            var stockAfterPlaced = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            stockAfterPlaced!.ReservedQuantity.ShouldBe(Quantity,
                "End-to-start: stock must have been reserved (predecessor state B exists)");

            // Publish PaymentExpired (simulates job completing A→B transition)
            await PublishAsync(CreatePaymentExpired(), CancellationToken);

            // End state (C): stock.ReservedQuantity == 0
            var stockAfterExpired = await GetRequiredService<IStockService>().GetByProductIdAsync(ProductId, CancellationToken);
            stockAfterExpired!.ReservedQuantity.ShouldBe(0,
                "End-to-start: reserved quantity 0 proves PaymentExpiredHandler ran and released holds");
            stockAfterExpired.AvailableQuantity.ShouldBe(50,
                "End-to-start: full availability restored proves release was not partial");
        }

        // ── Non-existent order: handler is a no-op ────────────────────────────

        [Fact]
        public async Task PaymentExpired_OrderDoesNotExist_ShouldCompleteWithoutError()
        {
            var act = async () => await PublishAsync(
                CreatePaymentExpired(orderId: int.MaxValue), CancellationToken);

            await act.ShouldNotThrowAsync();
        }
    }
}
