using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Inventory.Controllers
{
    [Area("Inventory")]
    [Authorize(Roles = MaintenanceRole)]
    public class StockController : BaseController
    {
        private readonly IStockQueryService _query;
        private readonly IStockService _stock;

        public StockController(IStockQueryService query, IStockService stock)
        {
            _query = query;
            _stock = stock;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
            => View(await _query.GetOverviewAsync(page, pageSize));

        [HttpGet]
        public async Task<IActionResult> Reservations(int page = 1, int pageSize = 20, string status = "active")
            => View(await _query.GetHoldsAsync(page, pageSize, status));

        [HttpGet]
        public async Task<IActionResult> Audit(int page = 1, int pageSize = 50)
            => View(await _query.GetAuditAsync(page, pageSize));

        [HttpGet]
        public IActionResult AdjustStock() => View();

        [HttpGet]
        public async Task<IActionResult> PendingAdjustments(CancellationToken ct = default)
            => View(await _query.GetPendingAdjustmentsAsync(ct));

        [Authorize(Roles = ManagingRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(int productId, int newQuantity)
        {
            await _stock.AdjustAsync(new AdjustStockDto(productId, newQuantity));
            TempData["Success"] = $"Korekta stanu dla produktu #{productId} zaplanowana.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = ManagingRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Release(int orderId, int productId, int quantity)
        {
            await _stock.ReleaseAsync(orderId, productId, quantity);
            TempData["Success"] = $"Rezerwacja zamówienia #{orderId} produktu #{productId} zwolniona.";
            return RedirectToAction(nameof(Reservations));
        }

        [Authorize(Roles = ManagingRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int orderId, int productId)
        {
            await _stock.ConfirmAsync(orderId, productId);
            TempData["Success"] = $"Rezerwacja zamówienia #{orderId} produktu #{productId} potwierdzona.";
            return RedirectToAction(nameof(Reservations));
        }
    }
}
