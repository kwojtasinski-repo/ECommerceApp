using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Presale.Checkout
{
    public class CheckoutServiceIntegrationTests : BcBaseTest<ICheckoutService>
    {
        private const int CustomerId = 1;
        private const int CurrencyId = 1;

        private static readonly CheckoutCustomer DefaultCustomer = new(
            "Jan", "Kowalski", "jan@test.com", "+48123456789",
            false, null, null,
            "ul. Testowa", "1", null,
            "00-001", "Warszawa", "Poland");

        private async Task<int> SeedProductAsync(decimal price = 49.99m)
        {
            var categoryRepo = GetRequiredService<ICategoryRepository>();
            var categoryId = await categoryRepo.AddAsync(Category.Create("CheckoutIntTest Category"));

            var productService = GetRequiredService<IProductService>();
            var productId = await productService.AddProduct(new CreateProductDto(
                "Checkout Int Test Product", price, "desc", categoryId.Value, new List<int>()));
            await productService.PublishProduct(productId);
            return productId;
        }

        private async Task SeedStockSnapshotAsync(int productId, int qty = 100)
        {
            var snapshotRepo = GetRequiredService<IStockSnapshotRepository>();
            await snapshotRepo.AddAsync(StockSnapshot.Create(productId, qty, DateTime.UtcNow));
        }

        // ── PlaceOrderAsync ───────────────────────────────────────────────

        [Fact]
        public async Task PlaceOrderAsync_NoActiveReservations_ReturnsNoSoftReservations()
        {
            var userId = new PresaleUserId("co-user-nosr");

            var result = await _service.PlaceOrderAsync(userId, CustomerId, CurrencyId, DefaultCustomer);

            result.ShouldBeOfType<CheckoutResult.NoSoftReservations>();
        }

        [Fact]
        public async Task PlaceOrderAsync_WithValidReservations_ReturnsSuccessAndCreatesOrder()
        {
            var userId = "co-user-success";
            var presaleUserId = new PresaleUserId(userId);

            var productId = await SeedProductAsync();
            await SeedStockSnapshotAsync(productId);

            var cartService = GetRequiredService<ICartService>();
            await cartService.SetCartItemAsync(new AddToCartDto(userId, productId, 2));

            var initiateResult = await _service.InitiateAsync(presaleUserId);
            initiateResult.ShouldBeOfType<InitiateCheckoutResult.Completed>();

            var result = await _service.PlaceOrderAsync(presaleUserId, CustomerId, CurrencyId, DefaultCustomer);

            result.ShouldBeOfType<CheckoutResult.Success>();
            var success = (CheckoutResult.Success)result;
            success.OrderId.ShouldBeGreaterThan(0);

            var orderService = GetRequiredService<IOrderService>();
            var orders = await orderService.GetOrdersByUserIdAsync(userId);
            orders.ShouldNotBeEmpty();
            orders.ShouldContain(o => o.Id == success.OrderId);
        }

        // ── InitiateAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task InitiateAsync_EmptyCart_ReturnsCartEmpty()
        {
            var userId = new PresaleUserId("co-user-emptycart");

            var result = await _service.InitiateAsync(userId);

            result.ShouldBeOfType<InitiateCheckoutResult.CartEmpty>();
        }

        [Fact]
        public async Task InitiateAsync_AllProductsUnavailable_ReturnsNothingReserved()
        {
            var userId = "co-user-unavail";
            var presaleUserId = new PresaleUserId(userId);
            var productId = await SeedProductAsync();
            // No snapshot seeded — HoldAsync will return false for this product

            var cartService = GetRequiredService<ICartService>();
            await cartService.SetCartItemAsync(new AddToCartDto(userId, productId, 1));

            var result = await _service.InitiateAsync(presaleUserId);

            result.ShouldBeOfType<InitiateCheckoutResult.NothingReserved>();
        }
    }
}
