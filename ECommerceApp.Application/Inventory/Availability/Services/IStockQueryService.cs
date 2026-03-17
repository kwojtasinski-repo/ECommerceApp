using ECommerceApp.Application.Inventory.Availability.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Services
{
    public interface IStockQueryService
    {
        Task<StockOverviewVm> GetOverviewAsync(int page, int pageSize, CancellationToken ct = default);
        Task<StockHoldsVm> GetHoldsAsync(int page, int pageSize, string statusFilter = "active", CancellationToken ct = default);
        Task<StockAuditVm> GetAuditAsync(int page, int pageSize, CancellationToken ct = default);
        Task<PendingAdjustmentsVm> GetPendingAdjustmentsAsync(CancellationToken ct = default);
    }
}
