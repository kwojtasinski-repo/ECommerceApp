using ECommerceApp.Application.Inventory.Availability.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Services
{
    public interface IStockService
    {
        Task<StockItemDto> GetByProductIdAsync(int productId, CancellationToken ct = default);
        Task InitializeStockAsync(int productId, int initialQuantity, CancellationToken ct = default);
        Task ReserveAsync(ReserveStockDto dto, CancellationToken ct = default);
        Task ReleaseAsync(int orderId, int productId, int quantity, CancellationToken ct = default);
        Task ConfirmAsync(int orderId, int productId, CancellationToken ct = default);
        Task FulfillAsync(int orderId, int productId, int quantity, CancellationToken ct = default);
        Task ReturnAsync(int productId, int quantity, CancellationToken ct = default);
        Task AdjustAsync(AdjustStockDto dto, CancellationToken ct = default);
    }
}
