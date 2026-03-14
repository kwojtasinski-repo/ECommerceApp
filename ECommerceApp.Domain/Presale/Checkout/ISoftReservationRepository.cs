using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Presale.Checkout
{
    public interface ISoftReservationRepository
    {
        Task<SoftReservation?> GetByIdAsync(SoftReservationId id, CancellationToken ct = default);
        Task<SoftReservation?> FindAsync(PresaleProductId productId, PresaleUserId userId, CancellationToken ct = default);
        Task<IReadOnlyList<SoftReservation>> GetByProductIdAsync(PresaleProductId productId, CancellationToken ct = default);
        Task<IReadOnlyList<SoftReservation>> GetByUserIdAsync(PresaleUserId userId, CancellationToken ct = default);
        Task<IReadOnlyList<SoftReservation>> GetActiveByUserIdAsync(PresaleUserId userId, CancellationToken ct = default);
        Task<IReadOnlyList<SoftReservation>> GetCommittedByUserIdAsync(PresaleUserId userId, CancellationToken ct = default);
        Task AddAsync(SoftReservation reservation, CancellationToken ct = default);
        Task DeleteAsync(SoftReservation reservation, CancellationToken ct = default);
        Task DeleteAllForProductAsync(PresaleProductId productId, CancellationToken ct = default);
        Task DeleteAllForUserAsync(PresaleUserId userId, CancellationToken ct = default);
        Task DeleteCommittedForUserAsync(PresaleUserId userId, CancellationToken ct = default);
        Task CommitAllForUserAsync(PresaleUserId userId, CancellationToken ct = default);
        Task RevertAllForUserAsync(PresaleUserId userId, CancellationToken ct = default);
    }
}
