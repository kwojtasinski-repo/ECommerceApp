using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class CartService : ICartService
    {
        private readonly ICartRepository _cartRepository;
        private readonly ICatalogClient _catalogClient;
        private readonly ISoftReservationService _softReservationService;

        public CartService(
            ICartRepository cartRepository,
            ICatalogClient catalogClient,
            ISoftReservationService softReservationService)
        {
            _cartRepository = cartRepository;
            _catalogClient = catalogClient;
            _softReservationService = softReservationService;
        }

        public async Task<CartVm?> GetCartAsync(string userId, CancellationToken ct = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);
            return cart is null ? null : MapToVm(cart);
        }

        public async Task<bool> AddToCartAsync(string userId, AddToCartDto dto, CancellationToken ct = default)
        {
            var unitPrice = await _catalogClient.GetUnitPriceAsync(dto.ProductId, ct);
            if (unitPrice is null)
                return false;

            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);
            if (cart is null)
            {
                cart = Cart.Create(userId);
                await _cartRepository.AddAsync(cart, ct);
            }

            cart.AddItem(dto.ProductId, dto.Quantity, unitPrice.Value);
            await _cartRepository.UpdateAsync(cart, ct);
            await _softReservationService.HoldAsync(dto.ProductId, userId, dto.Quantity, ct);
            return true;
        }

        public async Task<bool> UpdateQuantityAsync(string userId, int productId, int quantity, CancellationToken ct = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);
            if (cart is null)
                return false;

            var updated = cart.UpdateQuantity(productId, quantity);
            if (!updated)
                return false;

            await _cartRepository.UpdateAsync(cart, ct);
            await _softReservationService.HoldAsync(productId, userId, quantity, ct);
            return true;
        }

        public async Task<bool> RemoveItemAsync(string userId, int productId, CancellationToken ct = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);
            if (cart is null)
                return false;

            var removed = cart.RemoveItem(productId);
            if (!removed)
                return false;

            await _cartRepository.UpdateAsync(cart, ct);
            await _softReservationService.RemoveAsync(productId, userId, ct);
            return true;
        }

        public async Task ClearCartAsync(string userId, CancellationToken ct = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, ct);
            if (cart is null)
                return;

            cart.Clear();
            await _cartRepository.UpdateAsync(cart, ct);
        }

        private static CartVm MapToVm(Cart cart) => new CartVm
        {
            CartId = cart.Id.Value,
            UserId = cart.UserId,
            Items = cart.Items.Select(i => new CartItemVm
            {
                CartItemId = i.Id.Value,
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToArray()
        };
    }
}
