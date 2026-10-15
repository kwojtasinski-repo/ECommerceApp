using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Presale.Checkout
{
    public class CheckoutServiceIntegrationTests
        : BcBaseTest<ICheckoutService>, IClassFixture<MessageProcessingOperationsFixture>
    {
        private readonly MessageProcessingOperationsFixture _messageProcessing;

        public CheckoutServiceIntegrationTests(
            ITestOutputHelper output,
            MessageProcessingOperationsFixture messageProcessing) : base(output)
        {
            _messageProcessing = messageProcessing;
        }

        private const int CustomerId = 1;
        private const int CurrencyId = 1;

        private static readonly CheckoutCustomer DefaultCustomer = new(
            "Jan", "Kowalski", "jan@test.com", "+48123456789",
            false, null, null,
            "ul. Testowa", "1", null,
            "00-001", "Warszawa", "Poland");

        private async Task<int> SeedProductAsync(decimal price = 49.99m, CancellationToken ct = default)
        {
            var categoryRepo = GetRequiredService<ICategoryRepository>();
            var categoryId = await categoryRepo.AddAsync(Category.Create("CheckoutIntTest Category"));

            var productService = GetRequiredService<IProductService>();
            var productId = await productService.AddProduct(new CreateProductDto(
                "Checkout Int Test Product", price, "desc", categoryId.Value, new List<int>()));
            await productService.PublishProduct(productId);
            return productId;
        }

        private async Task SeedStockSnapshotAsync(int productId, int qty = 100, CancellationToken ct = default)
        {
            var snapshotRepo = GetRequiredService<IStockSnapshotRepository>();
            await snapshotRepo.AddAsync(StockSnapshot.Create(productId, qty, DateTime.UtcNow), CancellationToken);
        }

        private async Task<StockSnapshot> GetStockSnapshotAsync(int productId, CancellationToken ct)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IStockSnapshotRepository>();
            return await repo.FindByProductIdAsync(productId, ct);
        }

        // InitializeStockAsync enqueues StockAvailabilityChanged into the outbox; the dispatcher then
        // lets Presale's handler write the snapshot asynchronously. Waiting for the snapshot to reach
        // the real quantity proves the outbox message was dispatched and handled.
        private async Task InitializeRealStockAndAwaitSnapshotAsync(int productId, int quantity)
        {
            var stockService = GetRequiredService<IStockService>();
            await stockService.InitializeStockAsync(productId, quantity, CancellationToken);

            var snapshot = await _messageProcessing.WaitUntilAsync(
                new StockSnapshotQuantityOperation(this, productId, quantity));
            snapshot.AvailableQuantity.ShouldBe(quantity);
        }

        private async Task OverrideStockSnapshotAsync(int productId, int qty)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IStockSnapshotRepository>();
            var snapshot = await repo.FindByProductIdAsync(productId, CancellationToken);
            snapshot.Update(qty, DateTime.UtcNow);
            await repo.UpdateAsync(snapshot, CancellationToken);
        }

        private sealed class StockSnapshotQuantityOperation : IMessageProcessingOperation<StockSnapshot>
        {
            private readonly CheckoutServiceIntegrationTests _test;
            private readonly int _productId;
            private readonly int _expectedQuantity;

            public StockSnapshotQuantityOperation(
                CheckoutServiceIntegrationTests test,
                int productId,
                int expectedQuantity)
            {
                _test = test;
                _productId = productId;
                _expectedQuantity = expectedQuantity;
            }

            public Task<StockSnapshot> ReadAsync(CancellationToken cancellationToken)
            {
                return _test.GetStockSnapshotAsync(_productId, cancellationToken);
            }

            public bool IsCompleted(StockSnapshot state)
            {
                return state is not null && state.AvailableQuantity == _expectedQuantity;
            }

            public string Describe(StockSnapshot state)
            {
                return state is null
                    ? $"Stock snapshot for product {_productId} was not created."
                    : $"Stock snapshot for product {_productId} has quantity {state.AvailableQuantity}, expected {_expectedQuantity}.";
            }
        }

        // ── PlaceOrderAsync ───────────────────────────────────────────────

        [Fact]
        public async Task PlaceOrderAsync_NoActiveReservations_ReturnsNoSoftReservations()
        {
            var userId = new PresaleUserId("co-user-nosr");

            var result = await _service.PlaceOrderAsync(userId, CustomerId, CurrencyId, DefaultCustomer, CancellationToken);

            result.ShouldBeOfType<CheckoutResult.NoSoftReservations>();
        }

        [Fact]
        public async Task PlaceOrderAsync_WithValidReservations_ReturnsSuccessAndCreatesOrder()
        {
            var userId = "co-user-success";
            var presaleUserId = new PresaleUserId(userId);

            var productId = await SeedProductAsync(ct: CancellationToken);
            await SeedStockSnapshotAsync(productId, ct: CancellationToken);

            var cartService = GetRequiredService<ICartService>();
            await cartService.SetCartItemAsync(new AddToCartDto(userId, productId, 2), CancellationToken);

            var initiateResult = await _service.InitiateAsync(presaleUserId, CancellationToken);
            initiateResult.ShouldBeOfType<InitiateCheckoutResult.Completed>();

            var result = await _service.PlaceOrderAsync(presaleUserId, CustomerId, CurrencyId, DefaultCustomer, CancellationToken);

            result.ShouldBeOfType<CheckoutResult.Success>();
            var success = (CheckoutResult.Success)result;
            success.OrderId.ShouldBeGreaterThan(0);

            var orderService = GetRequiredService<IOrderService>();
            var orders = await orderService.GetOrdersByUserIdAsync(userId, CancellationToken);
            orders.ShouldNotBeEmpty();
            orders.ShouldContain(o => o.Id == success.OrderId);
        }

        [Fact]
        public async Task PlaceOrderAsync_ExceedsRealInventoryStock_ReturnsFailedAndDoesNotCreateOrder()
        {
            var userId = "co-user-lowstock";
            var presaleUserId = new PresaleUserId(userId);

            var productId = await SeedProductAsync(ct: CancellationToken);
            // Real Inventory stock is 1. Wait until the outbox has dispatched the resulting event and
            // Presale's handler has written its snapshot, so nothing can overwrite the override below.
            await InitializeRealStockAndAwaitSnapshotAsync(productId, quantity: 1);

            // Now make Presale's cached snapshot stale/optimistic — the real Inventory-side StockItem
            // is what should actually gate the order.
            await OverrideStockSnapshotAsync(productId, qty: 100);

            var cartService = GetRequiredService<ICartService>();
            await cartService.SetCartItemAsync(new AddToCartDto(userId, productId, 5), CancellationToken);

            var initiateResult = await _service.InitiateAsync(presaleUserId, CancellationToken);
            initiateResult.ShouldBeOfType<InitiateCheckoutResult.Completed>();

            var result = await _service.PlaceOrderAsync(presaleUserId, CustomerId, CurrencyId, DefaultCustomer, CancellationToken);

            result.ShouldBeOfType<CheckoutResult.OrderFailed>();

            var orderService = GetRequiredService<IOrderService>();
            var orders = await orderService.GetOrdersByUserIdAsync(userId, CancellationToken);
            orders.ShouldBeEmpty();
        }

        // ── InitiateAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task InitiateAsync_CartQuantityExceedsInSyncStock_ReturnsNothingReserved()
        {
            var userId = "co-user-insync-lowstock";
            var presaleUserId = new PresaleUserId(userId);

            var productId = await SeedProductAsync(ct: CancellationToken);
            await InitializeRealStockAndAwaitSnapshotAsync(productId, quantity: 1);

            var cartService = GetRequiredService<ICartService>();
            await cartService.SetCartItemAsync(new AddToCartDto(userId, productId, 5), CancellationToken);

            var result = await _service.InitiateAsync(presaleUserId, CancellationToken);

            result.ShouldBeOfType<InitiateCheckoutResult.NothingReserved>();
        }

        [Fact]
        public async Task InitiateAsync_EmptyCart_ReturnsCartEmpty()
        {
            var userId = new PresaleUserId("co-user-emptycart");

            var result = await _service.InitiateAsync(userId, CancellationToken);

            result.ShouldBeOfType<InitiateCheckoutResult.CartEmpty>();
        }

        [Fact]
        public async Task InitiateAsync_AllProductsUnavailable_ReturnsNothingReserved()
        {
            var userId = "co-user-unavail";
            var presaleUserId = new PresaleUserId(userId);
            var productId = await SeedProductAsync(ct: CancellationToken);
            // No snapshot seeded — HoldAsync will return false for this product

            var cartService = GetRequiredService<ICartService>();
            await cartService.SetCartItemAsync(new AddToCartDto(userId, productId, 1), CancellationToken);

            var result = await _service.InitiateAsync(presaleUserId, CancellationToken);

            result.ShouldBeOfType<InitiateCheckoutResult.NothingReserved>();
        }
    }
}

