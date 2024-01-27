using ECommerceApp.Application;
using ECommerceApp.Application.ViewModels.OrderItem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ECommerceApp.Infrastructure.Permissions;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.DTO;

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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}")]
        [HttpGet]
        public ActionResult<List<OrderItemDto>> GetAllOrderItems()
        {
            var orderItems = _orderItemService.GetOrderItems();
            return Ok(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}")]
        [HttpGet("{id}")]
        public ActionResult<OrderItemDetailsVm> GetOrderItem(int id)
        {
            var orderItem = _orderItemService.GetOrderItemDetails(id);
            if (orderItem is null)
            {
                return NotFound();
            }
            return Ok(orderItem);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet("by-user")]
        public ActionResult<List<OrderItemVm>> ShowMyCart()
        {
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userId2 = User.FindAll(ClaimTypes.NameIdentifier).ToList(); // 2 values in list
            var orderItems = _orderItemService.GetOrderItemsForRealization(userId);
            if (orderItems.Count() == 0)
            {
                return NotFound();
            }
            return Ok(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPut("{id:int}")]
        public IActionResult EditOrderItem(int id, [FromBody] AddOrderItemDto model)
        {
            model.Id = id;
            var modelExists = _orderItemService.OrderItemExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            var orderItem = model.AsOrderItemDto();
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            orderItem.UserId = userId;
            _orderItemService.UpdateOrderItem(orderItem);
            return Ok();
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult AddOrderItem([FromBody] AddOrderItemDto model)
        {
            if (model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var orderItem = model.AsOrderItemDto();
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            orderItem.UserId = userId;
            var id = _orderItemService.AddOrderItem(orderItem);
            return Ok(id);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet("by-items/{id}")]
        public ActionResult<List<OrderItemDto>> GetOrderItemsByItemId(int id)
        {
            var orderItems = _orderItemService.GetOrderItemsByItemId(id);
            return Ok(orderItems);
        }
    }
}
