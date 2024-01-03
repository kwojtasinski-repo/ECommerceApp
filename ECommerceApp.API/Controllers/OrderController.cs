using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application;
using ECommerceApp.Application.ViewModels.CouponUsed;
using ECommerceApp.Application.ViewModels.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Infrastructure.Permissions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Orders;

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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPut]
        public IActionResult EditOrder([FromBody] OrderDto model)
        {
            var vm = _orderService.GetOrderByIdReadOnly(model.Id);
            var modelExists = vm != null;
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }

            if (vm.IsPaid)
            {
                return Conflict();
            }
            vm.UserId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            vm.OrderItems.ForEach(oi => oi.UserId = vm.UserId);
            
            foreach(var id in model.OrderItems)
            {
                if (!vm.OrderItems.Any(o => o.Id == id.Id))
                {
                    vm.OrderItems.Add(new OrderItemVm { Id = id.Id });
                }
            }

            var orderItemsToRemove = new List<OrderItemVm>();
            foreach (var orderItem in vm.OrderItems)
            {
                var id = model.OrderItems.Where(oi => oi.Id == orderItem.Id).FirstOrDefault();

                if(id == null)
                {
                    orderItemsToRemove.Add(orderItem);
                }
            }

            foreach(var orderItem in orderItemsToRemove)
            {
                vm.OrderItems.Remove(orderItem);
            }

            _orderService.UpdateOrderWithExistedOrderItemsIds(vm);
            
            TryUseCoupon(model);

            return Ok();
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult AddOrder([FromBody] OrderDto model)
        {
            var order = model.AsVm();
            order.UserId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            order.OrderItems.ForEach(oi => oi.UserId = order.UserId);
            var id = _orderService.AddOrder(order);
            model.Id = id;
            TryUseCoupon(model);

            return Ok(id);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost("with-all-order-items")]
        public IActionResult AddOrderFromOrderItems([FromBody] OrderDto model)
        {
            model.OrderItems = new List<OrderItemsIdsVm>();
            var order = model.AsVm();
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            var orderItems = _orderItemService.GetOrderItemsNotOrderedByUserId(userId).ToList();
            order.UserId = userId;
            order.OrderItems = orderItems;
            var id = _orderService.AddOrder(order);
            order.OrderItems.ForEach(oi => oi.OrderId = id);
            _orderItemService.UpdateOrderItems(order.OrderItems);
            model.Id = id;
            model.OrderItems = orderItems.Select(oi => new OrderItemsIdsVm { Id = oi.Id }).ToList();
            TryUseCoupon(model);

            return Ok(id);
        }

        private void TryUseCoupon(OrderDto model)
        {
            if (!string.IsNullOrWhiteSpace(model.PromoCode))
            {
                var coupon = _couponService.GetCouponByCode(model.PromoCode);
                if (coupon != null)
                {
                    var couponUsed = new CouponUsedVm { CouponId = coupon.Id, OrderId = model.Id };
                    _couponUsedService.AddCouponUsed(couponUsed);
                }
            }
        }
    }
}
