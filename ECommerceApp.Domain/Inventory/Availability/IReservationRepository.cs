using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Inventory.Availability
{
    public interface IReservationRepository
    {
        Task<Reservation?> GetByOrderAndProductAsync(int orderId, int productId, CancellationToken ct = default);
        Task<IReadOnlyList<Reservation>> GetByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task AddAsync(Reservation reservation, CancellationToken ct = default);
        Task UpdateAsync(Reservation reservation, CancellationToken ct = default);
        Task DeleteAsync(Reservation reservation, CancellationToken ct = default);
    }
}
