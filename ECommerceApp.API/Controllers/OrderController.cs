using ECommerceApp.Application.Sales.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Authorize]
    [Route("api/orders")]
    public class OrderController : BaseController
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public async Task<IActionResult> GetOrders(CancellationToken ct = default)
        {
            var vm = await _orderService.GetAllOrdersAsync(100, 1, null, ct);
            return Ok(vm);
        }

        [HttpGet("by-customer/{customerId}")]
        public async Task<IActionResult> GetOrdersByCustomerId(int customerId, CancellationToken ct = default)
        {
            var orders = await _orderService.GetOrdersByCustomerIdAsync(customerId, ct);
            return Ok(orders);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrder(int id, CancellationToken ct = default)
        {
            var order = await _orderService.GetOrderDetailsAsync(id, ct);
            return order is null ? NotFound() : Ok(order);
        }

        [HttpGet("by-user")]
        public async Task<IActionResult> GetMyOrders(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var orders = await _orderService.GetOrdersByUserIdAsync(userId, ct);
            return Ok(orders);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPut("{id:int}")]
        public IActionResult EditOrder(int id)
            => StatusCode(StatusCodes.Status410Gone, new { error = "This endpoint has been removed. Use PUT /api/v2/orders." });

        [HttpPost]
        public IActionResult AddOrder()
            => StatusCode(StatusCodes.Status410Gone, new { error = "This endpoint has been removed. Use POST /api/v2/checkout/confirm." });

        [HttpPost("with-all-order-items")]
        public IActionResult AddOrderFromOrderItems()
            => StatusCode(StatusCodes.Status410Gone, new { error = "This endpoint has been removed. Use POST /api/v2/checkout/confirm." });
    }
}
