using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Application.ViewModels.Refund;
using ECommerceApp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class OrderItemController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IOrderItemService _orderItemService;

        public OrderItemController(IOrderService orderService, IOrderItemService orderItemService)
        {
            _orderService = orderService;
            _orderItemService = orderItemService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet]
        public IActionResult Index()
        {
            var orderItems = _orderItemService.GetOrderItems(20, 1, "");
            return View(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult ShowOrderItemsByItemId(int itemId)
        {
            var orderItems = _orderService.GetAllItemsOrderedByItemId(itemId, 20, 1);
            ViewBag.InputParameterId = itemId;
            return View(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult ShowOrderItemsByItemId(int itemId, int pageSize, int? pageNo)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }
            ViewBag.InputParameterId = itemId;
            var orderItems = _orderService.GetAllItemsOrderedByItemId(itemId, pageSize, pageNo.Value);
            return View(orderItems);
        }
        
        [Authorize(Roles = "Administrator, Admin, Manager")]
        public IActionResult ViewOrderItemDetails(int id)
        {
            var orderItem = _orderItemService.GetOrderItemDetails(id);
            if (orderItem is null)
            {
                return NotFound();
            }
            return View(orderItem);
        }
        
        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult OrderItemCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var value = _orderItemService.OrderItemCount(userId);
            return Json(new { count = value});
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult UpdateOrderItem([FromBody]OrderItemVm model)
        {
            _orderItemService.UpdateOrderItem(model);
            return Json(new { });
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddToCart([FromBody]OrderItemDto model)
        {
            var id = _orderItemService.AddOrderItem(model.AsVm());
            return Json(new { itemId = id });
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult AddToCart(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var itemOrderId = _orderItemService.AddOrderItem(id, userId);
            return Json(new { ItemOrderId = itemOrderId });
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult DeleteOrderItem(int id)
        {
            _orderService.DeleteOrderItem(id);
            return Json(new { });
        }
    }
}
