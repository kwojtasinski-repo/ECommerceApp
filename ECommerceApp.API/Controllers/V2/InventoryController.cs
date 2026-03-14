using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using static ECommerceApp.Application.Inventory.Availability.DTOs.ReserveStockResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/v2/inventory")]
    public class InventoryController : BaseController
    {
        private readonly IStockService _stock;

        public InventoryController(IStockService stock)
        {
            _stock = stock;
        }

        [HttpGet("stock/{productId:int}")]
        public async Task<IActionResult> GetStock(int productId, CancellationToken ct = default)
        {
            var item = await _stock.GetByProductIdAsync(productId, ct);
            return item is null ? NotFound() : Ok(item);
        }

        [HttpPost("stock/initialize")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> InitializeStock(
            [FromBody] InitializeStockRequest request,
            CancellationToken ct = default)
        {
            var ok = await _stock.InitializeStockAsync(request.ProductId, request.InitialQuantity, ct);
            return ok
                ? StatusCode(StatusCodes.Status201Created)
                : Conflict(new { error = "Stock already initialized for this product." });
        }

        [HttpPost("stock/adjust")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> AdjustStock([FromBody] AdjustStockDto dto, CancellationToken ct = default)
        {
            await _stock.AdjustAsync(dto, ct);
            return Ok();
        }

        [HttpPost("stock/return")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> ReturnStock(
            [FromBody] ReturnStockRequest request,
            CancellationToken ct = default)
        {
            var ok = await _stock.ReturnAsync(request.ProductId, request.Quantity, ct);
            return ok ? Ok() : NotFound();
        }

        [HttpPost("reservations")]
        public async Task<IActionResult> Reserve([FromBody] ReserveStockDto dto, CancellationToken ct = default)
        {
            var result = await _stock.ReserveAsync(dto, ct);
            return result == ReserveStockResult.Success
                ? Ok(new { result })
                : Conflict(new { error = "Insufficient stock.", result });
        }

        [HttpDelete("reservations")]
        public async Task<IActionResult> Release(
            [FromQuery] int orderId,
            [FromQuery] int productId,
            [FromQuery] int quantity,
            CancellationToken ct = default)
        {
            var ok = await _stock.ReleaseAsync(orderId, productId, quantity, ct);
            return ok ? NoContent() : NotFound();
        }
    }

    public sealed record InitializeStockRequest(int ProductId, int InitialQuantity);
    public sealed record ReturnStockRequest(int ProductId, int Quantity);
}
