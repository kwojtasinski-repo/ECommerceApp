using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.API.Controllers
{
    [Route("api/orders")]
    [Authorize]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly OrderServiceAbstract _orderService;

        public OrderController(OrderServiceAbstract orderService)
        {
            _orderService = orderService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet("all")]
        public ActionResult<List<OrderForListVm>> GetOrders()
        {
            var orders = _orderService.GetAllOrders();
            if (orders.Count == 0)
            {
                return NotFound();
            }
            return Ok(orders);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("by-customer/{customerId}")]
        public ActionResult<List<OrderForListVm>> GetOrdersByCustomerId(int customerId)
        {
            var orders = _orderService.GetAllOrdersByCustomerId(customerId);
            if (orders.Count == 0)
            {
                return NotFound();
            }
            return Ok(orders);
        }


        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
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

        

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("by-user")]
        public ActionResult<List<OrderForListVm>> GetMyOrders()
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orders = _orderService.GetAllOrdersByUserId(userId);
            if (orders == null)
            {
                return NotFound();
            }
            return Ok(orders);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPut]
        public IActionResult EditOrder([FromBody] OrderVm model)
        {
            var modelExists = _orderService.CheckIfOrderExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            var order = model.MapToNewOrderVm();
            order.UserId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            order.OrderItems.ForEach(oi => oi.UserId = order.UserId);
            _orderService.UpdateOrder(order);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddOrder([FromBody] OrderVm model)
        {
            var order = model.MapToNewOrderVm();
            order.UserId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            order.OrderItems.ForEach(oi => oi.UserId = order.UserId);
            var id = _orderService.AddOrder(order);
            return Ok(id);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("with-all-order-items")]
        public IActionResult AddOrderFromOrderItems([FromBody] OrderVm model)
        {
            var order = model.MapToNewOrderVm();
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orderItems = _orderService.GetOrderItemsNotOrderedByUserId(userId);
            order.UserId = userId;
            order.OrderItems = orderItems;
            var id = _orderService.AddOrder(order);
            order.OrderItems.ForEach(oi => oi.OrderId = id);
            _orderService.UpdateOrderItems(order.OrderItems);
            return Ok(id);
        }
    }
}
