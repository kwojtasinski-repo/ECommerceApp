using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Results;
using ECommerceApp.Application.Sales.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/v2/orders")]
    public class OrdersController : BaseController
    {
        private readonly IOrderService _orders;

        public OrdersController(IOrderService orders)
        {
            _orders = orders;
        }

        [HttpGet]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageNo = 1,
            [FromQuery] string? search = null,
            CancellationToken ct = default)
        {
            var vm = await _orders.GetAllOrdersAsync(pageSize, pageNo, search, ct);
            return Ok(vm);
        }

        [HttpGet("paid")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> GetAllPaid(
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageNo = 1,
            [FromQuery] string? search = null,
            CancellationToken ct = default)
        {
            var vm = await _orders.GetAllPaidOrdersAsync(pageSize, pageNo, search, ct);
            return Ok(vm);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
        {
            var vm = await _orders.GetOrderDetailsAsync(id, ct);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMine(CancellationToken ct = default)
        {
            var userId = GetUserId();
            var list = await _orders.GetOrdersByUserIdAsync(userId, ct);
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderDto dto, CancellationToken ct = default)
        {
            var result = await _orders.PlaceOrderAsync(dto, ct);
            return result.IsSuccess
                ? StatusCode(StatusCodes.Status201Created, new { orderId = result.OrderId })
                : BadRequest(new { error = result.FailureReason });
        }

        [HttpPut]
        [Authorize(Roles = ManagingRole)]
        public async Task<IActionResult> Update([FromBody] UpdateOrderDto dto, CancellationToken ct = default)
        {
            var result = await _orders.UpdateOrderAsync(dto, ct);
            return MapOperationResult(result);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = ManagingRole)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            var result = await _orders.DeleteOrderAsync(id, ct);
            return MapOperationResult(result);
        }

        [HttpPut("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id, CancellationToken ct = default)
        {
            var result = await _orders.CancelOrderAsync(id, ct);
            return MapOperationResult(result);
        }

        [HttpPut("{id:int}/deliver")]
        [Authorize(Roles = MaintenanceRole)]
        public async Task<IActionResult> MarkDelivered(int id, CancellationToken ct = default)
        {
            var result = await _orders.MarkAsDeliveredAsync(id, ct);
            return MapOperationResult(result);
        }

        private IActionResult MapOperationResult(OrderOperationResult result) => result switch
        {
            OrderOperationResult.Success => Ok(),
            OrderOperationResult.OrderNotFound => NotFound(new { error = "Order not found." }),
            OrderOperationResult.AlreadyPaid => Conflict(new { error = "Order is already paid." }),
            OrderOperationResult.AlreadyCancelled => Conflict(new { error = "Order is already cancelled." }),
            OrderOperationResult.AlreadyDelivered => Conflict(new { error = "Order is already delivered." }),
            OrderOperationResult.NotPaid => Conflict(new { error = "Order has not been paid." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
