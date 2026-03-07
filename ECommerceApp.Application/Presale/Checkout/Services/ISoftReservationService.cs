using ECommerceApp.Domain.Presale.Checkout;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    public interface ISoftReservationService
    {
        Task HoldAsync(int productId, string userId, int quantity, CancellationToken ct = default);
        Task<SoftReservation?> GetAsync(int productId, string userId, CancellationToken ct = default);
        Task RemoveAsync(int productId, string userId, CancellationToken ct = default);
        Task RemoveAllForProductAsync(int productId, CancellationToken ct = default);
    }
}
