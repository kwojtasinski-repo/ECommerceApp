using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Presale.Checkout
{
    public class CartServiceTests : BcBaseTest<ICartService>
    {
        public CartServiceTests(ITestOutputHelper output) : base(output) { }

        private const string TestUserId = "cart-test-user-001";

        // ── GetCartAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetCartAsync_EmptyCart_ShouldReturnNull()
        {
            var result = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);

            result.ShouldBeNull();
        }

        // ── SetCartItemAsync ─────────────────────────────────────────────

        [Fact]
        public async Task SetCartItemAsync_NewItem_ShouldAddToCart()
        {
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 3), CancellationToken);

            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);

            cart.ShouldNotBeNull();
            cart.UserId.ShouldBe(TestUserId);
            cart.Lines.Count.ShouldBe(1);
            cart.Lines[0].ProductId.ShouldBe(10);
            cart.Lines[0].Quantity.ShouldBe(3);
        }

        [Fact]
        public async Task SetCartItemAsync_MultipleItems_ShouldAddAllToCart()
        {
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 2), CancellationToken);
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 20, Quantity: 5), CancellationToken);

            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);

            cart.ShouldNotBeNull();
            cart.Lines.Count.ShouldBe(2);
        }

        // ── AddToCartAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task AddToCartAsync_ExistingItem_ShouldIncrementQuantity()
        {
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 3), CancellationToken);

            var result = await _service.AddToCartAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 2), CancellationToken);

            result.ShouldBeOfType<AddToCartResult.Success>();
            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);
            cart.ShouldNotBeNull();
            cart.Lines.Count.ShouldBe(1);
            cart.Lines[0].Quantity.ShouldBe(5); // 3 + 2
        }

        // ── RemoveAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task RemoveAsync_ExistingItem_ShouldRemoveFromCart()
        {
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 2), CancellationToken);
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 20, Quantity: 3), CancellationToken);

            await _service.RemoveAsync(new PresaleUserId(TestUserId), new PresaleProductId(10), CancellationToken);

            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);
            cart.ShouldNotBeNull();
            cart.Lines.Count.ShouldBe(1);
            cart.Lines[0].ProductId.ShouldBe(20);
        }

        // ── RemoveRangeAsync ─────────────────────────────────────────────

        [Fact]
        public async Task RemoveRangeAsync_MultipleItems_ShouldRemoveAll()
        {
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 1), CancellationToken);
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 20, Quantity: 1), CancellationToken);
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 30, Quantity: 1), CancellationToken);

            await _service.RemoveRangeAsync(
                new PresaleUserId(TestUserId),
                new List<PresaleProductId> { new(10), new(30) }, CancellationToken);

            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);
            cart.ShouldNotBeNull();
            cart.Lines.Count.ShouldBe(1);
            cart.Lines[0].ProductId.ShouldBe(20);
        }

        // ── ClearAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task ClearAsync_CartWithItems_ShouldEmptyCart()
        {
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 2), CancellationToken);
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 20, Quantity: 3), CancellationToken);

            await _service.ClearAsync(new PresaleUserId(TestUserId), CancellationToken);

            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);
            cart.ShouldBeNull();
        }

        // ── Full lifecycle ───────────────────────────────────────────────

        [Fact]
        public async Task FullLifecycle_AddRemoveClear_ShouldTrackCartState()
        {
            // Empty cart
            var empty = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);
            empty.ShouldBeNull();

            // Add items
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 1, Quantity: 2), CancellationToken);
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 2, Quantity: 3), CancellationToken);
            await _service.SetCartItemAsync(new AddToCartDto(TestUserId, ProductId: 3, Quantity: 1), CancellationToken);

            var withThree = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);
            withThree.ShouldNotBeNull();
            withThree.Lines.Count.ShouldBe(3);

            // Remove one
            await _service.RemoveAsync(new PresaleUserId(TestUserId), new PresaleProductId(2), CancellationToken);
            var withTwo = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);
            withTwo!.Lines.Count.ShouldBe(2);

            // Clear
            await _service.ClearAsync(new PresaleUserId(TestUserId), CancellationToken);
            var cleared = await _service.GetCartAsync(new PresaleUserId(TestUserId), CancellationToken);
            cleared.ShouldBeNull();
        }
    }
}

