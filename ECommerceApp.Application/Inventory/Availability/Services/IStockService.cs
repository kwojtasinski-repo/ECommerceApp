using ECommerceApp.Application.Inventory.Availability.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Services
{
    public interface IStockService
    {
        Task<StockItemDto?> GetByProductIdAsync(int productId, CancellationToken ct = default);
        IAsyncEnumerable<StockItemDto> GetByProductIdsAsync(IReadOnlyList<int> productIds, CancellationToken ct = default);
        Task<bool> InitializeStockAsync(int productId, int initialQuantity, CancellationToken ct = default);
        Task<ReserveStockResult> ReserveAsync(ReserveStockDto dto, CancellationToken ct = default);
        Task<bool> ReleaseAsync(int orderId, int productId, int quantity, CancellationToken ct = default);
        Task<bool> ConfirmAsync(int orderId, int productId, CancellationToken ct = default);
        Task ConfirmHoldsByOrderAsync(int orderId, CancellationToken ct = default);
        Task<bool> FulfillAsync(int orderId, int productId, int quantity, CancellationToken ct = default);
        Task<bool> ReturnAsync(int productId, int quantity, CancellationToken ct = default);
        Task AdjustAsync(AdjustStockDto dto, CancellationToken ct = default);
        Task<bool> WithdrawHoldAsync(int orderId, int productId, CancellationToken ct = default);
    }
}
