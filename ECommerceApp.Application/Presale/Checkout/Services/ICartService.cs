using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    public interface ICartService
    {
        Task<CartVm?> GetCartAsync(string userId, CancellationToken ct = default);
        Task<bool> AddToCartAsync(string userId, AddToCartDto dto, CancellationToken ct = default);
        Task<bool> UpdateQuantityAsync(string userId, int productId, int quantity, CancellationToken ct = default);
        Task<bool> RemoveItemAsync(string userId, int productId, CancellationToken ct = default);
        Task ClearCartAsync(string userId, CancellationToken ct = default);
    }
}
