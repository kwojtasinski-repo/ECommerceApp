using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    public interface ICartService
    {
        Task<AddToCartResult> AddToCartAsync(AddToCartDto dto, CancellationToken ct = default);
        Task SetCartItemAsync(AddToCartDto dto, CancellationToken ct = default);
        Task RemoveAsync(PresaleUserId userId, PresaleProductId productId, CancellationToken ct = default);
        Task RemoveRangeAsync(PresaleUserId userId, IReadOnlyList<PresaleProductId> productIds, CancellationToken ct = default);
        Task ClearAsync(PresaleUserId userId, CancellationToken ct = default);
        Task<CartVm?> GetCartAsync(PresaleUserId userId, CancellationToken ct = default);
    }
}
