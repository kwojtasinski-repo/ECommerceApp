using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Order;
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
        private readonly OrderServiceAbstract _orderService;

        public OrderItemController(OrderServiceAbstract orderService)
        {
            _orderService = orderService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet("all")]
        public ActionResult<List<OrderItemForListVm>> GetAllOrderItems()
        {
            var orderItems = _orderService.GetAllItemsOrdered();
            if (orderItems.Count == 0)
            {
                return NotFound();
            }
            return Ok(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet("{id}")]
        public ActionResult<OrderItemDetailsVm> GetOrderItem(int id)
        {
            var orderItem = _orderService.GetOrderItemDetail(id);
            if (orderItem == null)
            {
                return NotFound();
            }
            return Ok(orderItem);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public ActionResult<List<NewOrderItemVm>> ShowMyCart()
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userId2 = User.FindAll(ClaimTypes.NameIdentifier).ToList(); // 2 values in list
            var orderItems = _orderService.GetOrderItemsNotOrderedByUserId(userId);
            if (orderItems == null)
            {
                return NotFound();
            }
            return Ok(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPut]
        public IActionResult EditOrderItem([FromBody] OrderItemForListVm model)
        {
            var modelExists = _orderService.CheckIfOrderItemExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _orderService.UpdateOrderItem(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddOrderItem([FromBody] NewOrderItemVm model)
        {
            if (!ModelState.IsValid || model.Id != 0 || model.UserId != null)
            {
                return Conflict(ModelState);
            }
            model.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _orderService.AddOrderItem(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet("by-items/{id}")]
        public ActionResult<List<OrderItemForListVm>> GetOrderItemsByItemId(int id)
        {
            var orderItems = _orderService.GetAllItemsOrderedByItemId(id);
            if (orderItems == null)
            {
                return NotFound();
            }
            return Ok(orderItems);
        }
    }
}
