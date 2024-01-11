using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ECommerceApp.Application.ViewModels.CouponUsed;
using ECommerceApp.Application.ViewModels.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.Infrastructure.Permissions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.DTO;

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
            return Ok(orders);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet("by-customer/{customerId}")]
        public ActionResult<List<OrderForListVm>> GetOrdersByCustomerId(int customerId)
        {
            var orders = _orderService.GetAllOrdersByCustomerId(customerId);
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPut("{id:int}")]
        public IActionResult EditOrder(int id, [FromBody] UpdateOrderDto model)
        {
            model.Id = id;
            _orderService.UpdateOrder(model);
            return Ok();
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult AddOrder([FromBody] AddOrderDto model)
        {
            return Ok(_orderService.AddOrder(model));
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost("with-all-order-items")]
        public IActionResult AddOrderFromOrderItems([FromBody] AddOrderFromCartDto model)
        {
            var id = _orderService.AddOrderFromCart(model);
            return Ok(id);
        }

        private void TryUseCoupon(AddOrderDto model)
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
