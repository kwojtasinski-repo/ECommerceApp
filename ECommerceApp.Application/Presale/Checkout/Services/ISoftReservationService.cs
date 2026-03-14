using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    public interface ISoftReservationService
    {
        Task<bool> HoldAsync(int productId, string userId, int quantity, CancellationToken ct = default);
        Task<SoftReservation?> GetAsync(int productId, string userId, CancellationToken ct = default);
        Task RemoveAsync(int productId, string userId, CancellationToken ct = default);
        Task RemoveAllForProductAsync(int productId, CancellationToken ct = default);
        Task RemoveAllForUserAsync(string userId, CancellationToken ct = default);
        Task RemoveActiveForUserAsync(string userId, CancellationToken ct = default);
        Task RemoveCommittedForUserAsync(string userId, CancellationToken ct = default);
        Task CommitAllForUserAsync(PresaleUserId userId, CancellationToken ct = default);
        Task RevertAllForUserAsync(PresaleUserId userId, CancellationToken ct = default);
        Task<IReadOnlyList<SoftReservation>> GetAllForUserAsync(PresaleUserId userId, CancellationToken ct = default);
        Task<IReadOnlyList<SoftReservationPriceChangeVm>> GetPriceChangesAsync(PresaleUserId userId, CancellationToken ct = default);
    }
}
