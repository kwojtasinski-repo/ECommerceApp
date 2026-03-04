using ECommerceApp.Application.Inventory.Availability.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Services
{
    public interface ICheckoutSoftHoldService
    {
        Task HoldAsync(int productId, string userId, int quantity, CancellationToken ct = default);
        Task<SoftHold?> GetAsync(int productId, string userId, CancellationToken ct = default);
        Task RemoveAsync(int productId, string userId, CancellationToken ct = default);
    }
}
