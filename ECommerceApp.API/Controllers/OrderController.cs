using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application;
using ECommerceApp.Application.ViewModels.CouponUsed;
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
        private readonly IOrderService _orderService;
        private readonly ICouponService _couponService;
        private readonly ICouponUsedService _couponUsedService;
        private readonly IOrderItemService _orderItemService;

        public OrderController(IOrderService orderService, ICouponService couponService, ICouponUsedService couponUsedService, IOrderItemService orderItemService)
        {
            _orderService = orderService;
            _couponService = couponService;
            _couponUsedService = couponUsedService;
            _orderItemService = orderItemService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
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
        public IActionResult EditOrder([FromBody] OrderDto model)
        {
            var vm = _orderService.GetOrderById(model.Id);
            var modelExists = vm != null;
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }

            if (vm.IsPaid)
            {
                return Conflict();
            }

            var couponUsedId = FindCoupon(model);
            if (couponUsedId != null)
            {
                vm.CouponUsedId = couponUsedId;
                vm.OrderItems.ForEach(oi => oi.CouponUsedId = couponUsedId);
            }

            vm.UserId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            vm.OrderItems.ForEach(oi => oi.UserId = vm.UserId);
            _orderService.UpdateOrder(vm);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddOrder([FromBody] OrderDto model)
        {
            var order = model.AsVm();

            var couponUsedId = FindCoupon(model);
            if (couponUsedId != null)
            {
                order.CouponUsedId = couponUsedId;
                order.OrderItems.ForEach(oi => oi.CouponUsedId = couponUsedId);
            }

            order.UserId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            order.OrderItems.ForEach(oi => oi.UserId = order.UserId);
            var id = _orderService.AddOrder(order);
            return Ok(id);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("with-all-order-items")]
        public IActionResult AddOrderFromOrderItems([FromBody] OrderDto model)
        {
            var order = model.AsVm();
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            //var orderItems = _orderService.GetOrderItemsNotOrderedByUserId(userId);
            var orderItems = _orderItemService.GetOrderItems(oi => oi.UserId == userId && oi.OrderId == null).ToList();
            order.UserId = userId;
            order.OrderItems = orderItems;
            var id = _orderService.AddOrder(order);
            order.OrderItems.ForEach(oi => oi.OrderId = id);
            _orderItemService.UpdateOrderItems(order.OrderItems);
            return Ok(id);
        }

        private int? FindCoupon(OrderDto model)
        {
            int? couponUsedId = null;

            if (!string.IsNullOrWhiteSpace(model.PromoCode))
            {
                var coupon = _couponService.GetCouponByCode(model.PromoCode);
                if (coupon != null)
                {
                    var couponUsed = new CouponUsedVm { CouponId = coupon.Id, OrderId = model.Id };
                    couponUsedId = _couponUsedService.AddCouponUsed(couponUsed);
                }
            }

            return couponUsedId;
        }
    }
}
