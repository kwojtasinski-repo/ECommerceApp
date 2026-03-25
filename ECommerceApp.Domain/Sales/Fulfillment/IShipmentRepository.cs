using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Sales.Fulfillment
{
    public interface IShipmentRepository
    {
        Task<Shipment?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<int> AddAsync(Shipment shipment, CancellationToken ct = default);
        Task UpdateAsync(Shipment shipment, CancellationToken ct = default);
        Task<IReadOnlyList<Shipment>> GetByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task<IReadOnlyList<Shipment>> GetAllAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default);
        Task<int> CountAsync(string searchString, CancellationToken ct = default);
    }
}
