using ECommerceApp.Application.Sales.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/order-items")]
    public class OrderItemController : BaseController
    {
        private readonly IOrderItemService _orderItemService;

        public OrderItemController(IOrderItemService orderItemService)
        {
            _orderItemService = orderItemService;
        }

        [Authorize(Roles = $"{ManagingRole}")]
        [HttpGet]
        public async Task<IActionResult> GetAllOrderItems(CancellationToken ct = default)
        {
            var vm = await _orderItemService.GetAllPagedAsync(100, 1, null, ct);
            return Ok(vm);
        }

        [Authorize(Roles = $"{ManagingRole}")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderItem(int id, CancellationToken ct = default)
        {
            var orderItem = await _orderItemService.GetByIdAsync(id, ct);
            return orderItem is null ? NotFound() : Ok(orderItem);
        }

        [HttpGet("by-user")]
        public async Task<IActionResult> ShowMyCart(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var orderItems = await _orderItemService.GetCartItemsByUserIdAsync(userId, ct);
            return Ok(orderItems);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPut("{id:int}")]
        public IActionResult EditOrderItem(int id)
            => StatusCode(StatusCodes.Status410Gone, new { error = "This endpoint has been removed. Use DELETE /api/v2/cart/items/{productId} then POST /api/v2/cart/items." });

        [HttpPost]
        public IActionResult AddOrderItem()
            => StatusCode(StatusCodes.Status410Gone, new { error = "This endpoint has been removed. Use POST /api/v2/cart/items." });

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet("by-items/{id}")]
        public IActionResult GetOrderItemsByItemId(int id)
            => StatusCode(StatusCodes.Status410Gone, new { error = "This endpoint has been removed. Use GET /api/v2/orders." });
    }
}
