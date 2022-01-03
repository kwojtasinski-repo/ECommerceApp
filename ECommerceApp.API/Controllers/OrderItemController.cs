using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.OrderItem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Route("api/order-items")]
    [ApiController]
    public class OrderItemController : ControllerBase
    {
        private readonly IOrderItemService _orderItemService;

        public OrderItemController(IOrderItemService orderItemService)
        {
            _orderItemService = orderItemService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet]
        public ActionResult<List<OrderItemVm>> GetAllOrderItems()
        {
            var orderItems = _orderItemService.GetOrderItems(oi => true);
            if (orderItems.Count() == 0)
            {
                return NotFound();
            }
            return Ok(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet("{id}")]
        public ActionResult<OrderItemDetailsVm> GetOrderItem(int id)
        {
            var orderItem = _orderItemService.GetOrderItemDetails(id);
            if (orderItem == null)
            {
                return NotFound();
            }
            return Ok(orderItem);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("by-user")]
        public ActionResult<List<OrderItemVm>> ShowMyCart()
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userId2 = User.FindAll(ClaimTypes.NameIdentifier).ToList(); // 2 values in list
            var orderItems = _orderItemService.GetOrderItems(oi => oi.UserId == userId && oi.OrderId == null);
            if (orderItems.Count() == 0)
            {
                return NotFound();
            }
            return Ok(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut]
        public IActionResult EditOrderItem([FromBody] OrderItemDto model)
        {
            var modelExists = _orderItemService.OrderItemExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            var orderItem = model.AsVm();
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            orderItem.UserId = userId;
            _orderItemService.UpdateOrderItem(orderItem);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddOrderItem([FromBody] OrderItemDto model)
        {
            if (model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var orderItem = model.AsVm();
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            orderItem.UserId = userId;
            var id = _orderItemService.AddOrderItem(orderItem);
            return Ok(id);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet("by-items/{id}")]
        public ActionResult<List<OrderItemForListVm>> GetOrderItemsByItemId(int id)
        {
            var orderItems = _orderItemService.GetOrderItems(oi => oi.ItemId == id);
            if (orderItems.Count() == 0)
            {
                return NotFound();
            }
            return Ok(orderItems);
        }
    }
}
