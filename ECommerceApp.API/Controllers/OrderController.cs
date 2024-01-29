using System.Collections.Generic;
using ECommerceApp.Application.ViewModels.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.DTO;

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
        public ActionResult<List<OrderForListVm>> GetOrders()
        {
            var orders = _orderService.GetAllOrders();
            return Ok(orders);
        }

        [HttpGet("by-customer/{customerId}")]
        public ActionResult<List<OrderForListVm>> GetOrdersByCustomerId(int customerId)
        {
            var orders = _orderService.GetAllOrdersByCustomerId(customerId);
            return Ok(orders);
        }

        [HttpGet("{id}")]
        public ActionResult<OrderDetailsVm> GetOrder(int id)
        {
            var order = _orderService.GetOrderDetail(id);
            if (order == null)
            {
                return NotFound();
            }
            return Ok(order);
        }

        [HttpGet("by-user")]
        public ActionResult<List<OrderForListVm>> GetMyOrders()
        {
            var userId = GetUserId();
            var orders = _orderService.GetAllOrdersByUserId(userId);
            if (orders == null)
            {
                return NotFound();
            }
            return Ok(orders);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPut("{id:int}")]
        public IActionResult EditOrder(int id, [FromBody] UpdateOrderDto model)
        {
            model.Id = id;
            _orderService.UpdateOrder(model);
            return Ok();
        }

        [HttpPost]
        public IActionResult AddOrder([FromBody] AddOrderDto model)
        {
            return Ok(_orderService.AddOrder(model));
        }

        [HttpPost("with-all-order-items")]
        public IActionResult AddOrderFromOrderItems([FromBody] AddOrderFromCartDto model)
        {
            var id = _orderService.AddOrderFromCart(model);
            return Ok(id);
        }
    }
}
