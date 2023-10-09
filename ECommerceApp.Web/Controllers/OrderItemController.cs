using System;
using System.Security.Claims;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application;
using ECommerceApp.Application.ViewModels.OrderItem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.Infrastructure.Permissions;

namespace ECommerceApp.Web.Controllers
{
    public class OrderItemController : Controller
    {
        private readonly IOrderItemService _orderItemService;

        public OrderItemController(IOrderItemService orderItemService)
        {
            _orderItemService = orderItemService;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}")]
        [HttpGet]
        public IActionResult Index()
        {
            var orderItems = _orderItemService.GetOrderItems(20, 1, "");
            return View(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var orderItems = _orderItemService.GetOrderItems(pageSize, pageNo.Value, searchString);
            return View(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult ShowOrderItemsByItemId(int itemId)
        {
            var orderItems = _orderItemService.GetAllItemsOrderedByItemId(itemId, 20, 1);
            ViewBag.InputParameterId = itemId;
            return View(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult ShowOrderItemsByItemId(int itemId, int pageSize, int? pageNo)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }
            ViewBag.InputParameterId = itemId;
            var orderItems = _orderItemService.GetAllItemsOrderedByItemId(itemId, pageSize, pageNo.Value);
            return View(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}")]
        public IActionResult ViewOrderItemDetails(int id)
        {
            var orderItem = _orderItemService.GetOrderItemDetails(id);
            if (orderItem is null)
            {
                return NotFound();
            }
            return View(orderItem);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult OrderItemCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var value = _orderItemService.OrderItemCount(userId);
            return Json(new { count = value});
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPut("/{id}")]
        public IActionResult UpdateOrderItem(int id, [FromBody]OrderItemVm model)
        {
            model.Id = id;
            model.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _orderItemService.UpdateOrderItem(model);
            return Json(new { Status = "Updated" });
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult AddToCart([FromBody]OrderItemDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var dto = model.AsVm();
            dto.UserId = userId;
            var id = _orderItemService.AddOrderItem(dto);
            return Json(new { itemId = id });
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        public IActionResult DeleteOrderItem(int id)
        {
            _orderItemService.DeleteOrderItem(id);
            return Json(new { });
        }
    }
}
