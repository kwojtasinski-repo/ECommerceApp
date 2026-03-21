using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Presale.Checkout
{
    public class CartServiceTests : BcBaseTest<ICartService>
    {
        private const string TestUserId = "cart-test-user-001";

        // ── GetCartAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetCartAsync_EmptyCart_ShouldReturnNull()
        {
            var result = await _service.GetCartAsync(new PresaleUserId(TestUserId));

            result.ShouldBeNull();
        }

        // ── AddOrUpdateAsync ─────────────────────────────────────────────

        [Fact]
        public async Task AddOrUpdateAsync_NewItem_ShouldAddToCart()
        {
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 3));

            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId));

            cart.ShouldNotBeNull();
            cart.UserId.ShouldBe(TestUserId);
            cart.Lines.Count.ShouldBe(1);
            cart.Lines[0].ProductId.ShouldBe(10);
            cart.Lines[0].Quantity.ShouldBe(3);
        }

        [Fact]
        public async Task AddOrUpdateAsync_MultipleItems_ShouldAddAllToCart()
        {
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 2));
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 20, Quantity: 5));

            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId));

            cart.ShouldNotBeNull();
            cart.Lines.Count.ShouldBe(2);
        }

        // ── RemoveAsync ──────────────────────────────────────────────────

        [Fact]
        public async Task RemoveAsync_ExistingItem_ShouldRemoveFromCart()
        {
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 2));
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 20, Quantity: 3));

            await _service.RemoveAsync(new PresaleUserId(TestUserId), new PresaleProductId(10));

            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId));
            cart.ShouldNotBeNull();
            cart.Lines.Count.ShouldBe(1);
            cart.Lines[0].ProductId.ShouldBe(20);
        }

        // ── RemoveRangeAsync ─────────────────────────────────────────────

        [Fact]
        public async Task RemoveRangeAsync_MultipleItems_ShouldRemoveAll()
        {
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 1));
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 20, Quantity: 1));
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 30, Quantity: 1));

            await _service.RemoveRangeAsync(
                new PresaleUserId(TestUserId),
                new List<PresaleProductId> { new(10), new(30) });

            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId));
            cart.ShouldNotBeNull();
            cart.Lines.Count.ShouldBe(1);
            cart.Lines[0].ProductId.ShouldBe(20);
        }

        // ── ClearAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task ClearAsync_CartWithItems_ShouldEmptyCart()
        {
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 10, Quantity: 2));
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 20, Quantity: 3));

            await _service.ClearAsync(new PresaleUserId(TestUserId));

            var cart = await _service.GetCartAsync(new PresaleUserId(TestUserId));
            cart.ShouldBeNull();
        }

        // ── Full lifecycle ───────────────────────────────────────────────

        [Fact]
        public async Task FullLifecycle_AddRemoveClear_ShouldTrackCartState()
        {
            // Empty cart
            var empty = await _service.GetCartAsync(new PresaleUserId(TestUserId));
            empty.ShouldBeNull();

            // Add items
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 1, Quantity: 2));
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 2, Quantity: 3));
            await _service.AddOrUpdateAsync(new AddToCartDto(TestUserId, ProductId: 3, Quantity: 1));

            var withThree = await _service.GetCartAsync(new PresaleUserId(TestUserId));
            withThree.ShouldNotBeNull();
            withThree.Lines.Count.ShouldBe(3);

            // Remove one
            await _service.RemoveAsync(new PresaleUserId(TestUserId), new PresaleProductId(2));
            var withTwo = await _service.GetCartAsync(new PresaleUserId(TestUserId));
            withTwo!.Lines.Count.ShouldBe(2);

            // Clear
            await _service.ClearAsync(new PresaleUserId(TestUserId));
            var cleared = await _service.GetCartAsync(new PresaleUserId(TestUserId));
            cleared.ShouldBeNull();
        }
    }
}
