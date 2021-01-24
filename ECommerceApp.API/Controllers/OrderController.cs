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
    [Route("api/[controller]")]
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
        [HttpGet("Order/All")]
        public ActionResult<List<OrderForListVm>> GetOrders()
        {
            var orders = _orderService.GetAllOrders();
            if (orders.Count == 0)
            {
                return NotFound();
            }
            return Ok(orders);
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet("Payment/All")]
        public ActionResult<List<PaymentForListVm>> GetPayments()
        {
            var payments = _orderService.GetAllPayments();
            if (payments.Count == 0)
            {
                return NotFound();
            }
            return Ok(payments);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet("Refund/All")]
        public ActionResult<List<RefundForListVm>> GetRefunds()
        {
            var refunds = _orderService.GetAllRefunds();
            if (refunds.Count == 0)
            {
                return NotFound();
            }
            return Ok(refunds);
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet("OrderItem/All")]
        public ActionResult<List<OrderItemForListVm>> GetAllOrderItems()
        {
            var orderItems = _orderService.GetAllItemsOrdered();
            if (orderItems.Count == 0)
            {
                return NotFound();
            }
            return Ok(orderItems);
        }


        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("Order/Customer/{id}")]
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
        [HttpGet("Order/Get/{id}")]
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
        [HttpGet("Payment/Get/{id}")]
        public ActionResult<PaymentDetailsVm> GetPayment(int id)
        {
            var payment = _orderService.GetPaymentDetail(id);
            if (payment == null)
            {
                return NotFound();
            }
            return Ok(payment);
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet("OrderItem/Get/{id}")]
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
        [HttpGet("Refund/Get/{id}")]
        public ActionResult<RefundDetailsVm> GetRefund(int id)
        {
            var refund = _orderService.GetRefundDetail(id);
            if (refund == null)
            {
                return NotFound();
            }
            return Ok(refund);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("Order/User/")]
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet("OrderItem/All/Items/{id}")]
        public ActionResult<List<OrderItemForListVm>> GetOrderItemsByItemId(int id)
        {
            var orderItems = _orderService.GetAllItemsOrderedByItemId(id);
            if (orderItems == null)
            {
                return NotFound();
            }
            return Ok(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet("OrderItem/All/User/")]
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("Order/Edit/{id}")]
        public IActionResult EditOrder([FromBody]NewOrderVm model)
        {
            var modelExists = _orderService.CheckIfOrderExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _orderService.UpdateOrder(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost("Payment/Edit/{id}")]
        public IActionResult EditPayment([FromBody]NewPaymentVm model)
        {
            var modelExists = _orderService.CheckIfPaymentExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _orderService.UpdatePayment(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost("Refund/Edit/{id}")]
        public IActionResult EditRefund([FromBody] NewRefundVm model)
        {
            var modelExists = _orderService.CheckIfRefundExists(model.Id);
            if (!ModelState.IsValid || !modelExists)
            {
                return Conflict(ModelState);
            }
            _orderService.UpdateRefund(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost("OrderItem/Edit/{id}")]
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
        [HttpPost("OrderItem/New")]
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
        

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("Order/New")]
        public IActionResult AddOrder([FromBody] NewOrderVm model)
        {
            if (!ModelState.IsValid || model.Id != 0 || model.UserId != null || model.Number != 0)
            {
                return Conflict(ModelState);
            }
            Random random = new Random();
            model.Number = random.Next(100, 10000);
            model.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _orderService.AddOrder(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("Order/FromOrderItem/New")]
        public IActionResult AddOrderFromOrderItems([FromBody] NewOrderVm model)
        {
            if (!ModelState.IsValid || model.Id != 0 || model.UserId != null || model.Number != 0)
            {
                return Conflict(ModelState);
            }
            Random random = new Random();
            model.Number = random.Next(100, 10000);
            var userId = User.FindAll(ClaimTypes.NameIdentifier).SingleOrDefault(c => c.Value != User.Identity.Name).Value;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orderItems = _orderService.GetOrderItemsNotOrderedByUserId(userId);
            model.UserId = userId;
            model.OrderItems = orderItems;
            var id = _orderService.AddOrder(model);
            model.OrderItems.ForEach(oi => oi.OrderId = id);
            _orderService.UpdateOrderItems(model.OrderItems);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("Payment/New")]
        public IActionResult AddPayment([FromBody] NewPaymentVm model)
        {
            if (!ModelState.IsValid || model.Id != 0 || model.Number != 0)
            {
                return Conflict(ModelState);
            }
            Random random = new Random();
            model.Number = random.Next(0, 1000);
            var id = _orderService.AddPayment(model);
            return Ok();
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost("Payment/New")]
        public IActionResult AddRefund([FromBody] NewRefundVm model)
        {
            if (!ModelState.IsValid || model.Id != 0)
            {
                return Conflict(ModelState);
            }
            var id = _orderService.AddRefund(model);
            return Ok();
        }
    }
}
