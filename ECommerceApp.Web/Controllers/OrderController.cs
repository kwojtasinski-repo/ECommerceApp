using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ECommerceApp.Application;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.Refund;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Infrastructure.Permissions;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.Services.Refunds;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.DTO;

namespace ECommerceApp.Web.Controllers
{
    public class OrderController : BaseController
    {
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly IRefundService _refundService;
        private readonly IOrderItemService _orderItemService;
        private readonly IItemService _itemService;
        private readonly ICustomerService _customerService;
        private readonly ICouponService _couponService;

        public OrderController(IOrderService orderService, IPaymentService paymentService, IRefundService refundService, IOrderItemService orderItemService, IItemService itemService, ICustomerService customerService, ICouponService couponService)
        {
            _orderService = orderService;
            _paymentService = paymentService;
            _refundService = refundService;
            _orderItemService = orderItemService;
            _itemService = itemService;
            _customerService = customerService;
            _couponService = couponService;
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult Index()
        {
            var model = _orderService.GetAllOrders(20, 1, "");
            return View(model);
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

            var model = _orderService.GetAllOrders(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult AddOrder()
        {
            return View(_orderService.InitOrder());
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult AddOrder(OrderVm model)
        {
            var id = _orderService.AddOrder(new AddOrderDto
            {
                Id = model.Order.Id,
                CustomerId = model.Order.CustomerId,
                OrderItems = model.Order.OrderItems?.Select(oi => new OrderItemsIdsDto { Id = oi.Id }).ToList() 
                    ?? new List<OrderItemsIdsDto>(),
            });
            return RedirectToAction("AddOrderDetails", new { orderId = id });
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult AddOrderItemToCart()
        {
            var orderItems = new NewOrderItemVm();
            var items = _itemService.GetItemsAddToCart();
            orderItems.Items = items;
            orderItems.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.ItemsJson = Json(items);
            return View(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult AddOrderItemToCart(OrderItemDto model)
        {
            var id = _orderItemService.AddOrderItem(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult OrderRealization()
        {
            return View(_orderService.InitOrder());
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult OrderRealization(OrderVm model)
        {
            int orderId;
            var addOrderDto = new AddOrderDto
            {
                Id = model.Order.Id,
                CustomerId = model.Order.CustomerId,
                PromoCode = model.PromoCode,
                OrderItems = model.Order.OrderItems?.Select(oi => new OrderItemsIdsDto { Id = oi.Id }).ToList()
                    ?? new List<OrderItemsIdsDto>()
            };
            if (model.CustomerData)
            {
                orderId = _orderService.AddOrder(addOrderDto);
            }
            else
            {
                var customerId = _customerService.AddCustomerDetails(model.NewCustomer);
                addOrderDto.CustomerId = customerId;
                orderId = _orderService.AddOrder(addOrderDto);
            }
            model.Order.Id = orderId;
            UseCouponIfEntered(new NewOrderVm
            {
                Id = orderId,
                Number = model.Order.Number,
                RefCode = model.PromoCode,
                Ordered = model.Order.Ordered,
                CurrencyId = model.Order.CurrencyId,
                CustomerId = model.Order.CustomerId,
                Cost = model.Order.Cost,
                UserId = model.Order.UserId,
            });
            return RedirectToAction("AddOrderSummary", new { id = orderId });
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult AddOrderDetails(int orderId)
        {
            var order = _orderService.GetOrderForRealization(orderId);
            var customer = _customerService.GetCustomerInformationById(order.CustomerId);
            order.Items = _itemService.GetAllItems(i => true).ToList();
            ViewBag.CustomerInformation = customer.Information;
            return View(order);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult AddOrderDetails(NewOrderVm model)
        {
            model.Cost = Convert.ToDecimal(model.CostToConvert);

            UseCouponIfEntered(model);

            if (model.OrderItems.Count > 0)
            {
                model.OrderItems.ForEach(oi =>
                {
                    oi.UserId = model.UserId;
                });
                _orderService.UpdateOrder(model.AsOrderDto());
            }
            else
            {
                DeleteOrder(model.Id);
            }

            return RedirectToAction("AddOrderSummary", new { id = model.Id });
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult AddOrderSummary(int id)
        {
            var order = _orderService.GetOrderForRealization(id);
            return View(order);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult ShowMyCart()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orderItems = _orderItemService.GetOrderItemsNotOrderedByUserId(userId, 20, 1);
            return View(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult ShowMyCart(int pageSize, int? pageNo)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orderItems = _orderItemService.GetOrderItemsNotOrderedByUserId(userId, pageSize, pageNo.Value);
            return View(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult ShowOrdersByCustomerId(int customerId)
        {
            var orderItems = _orderService.GetAllOrdersByCustomerId(customerId, 20, 1);
            ViewBag.InputParameterId = customerId;
            return View(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult ShowOrdersByCustomerId(int customerId, int pageSize, int? pageNo)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }
            ViewBag.InputParameterId = customerId;
            var orderItems = _orderService.GetAllOrdersByCustomerId(customerId, pageSize, pageNo.Value);
            return View(orderItems);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult EditOrder(int id)
        {
            var order = _orderService.GetOrderForRealization(id);
            if (order is null)
            {
                return NotFound();
            }
            var items = _itemService.GetAllItems(i => true).ToList();
            order.Items = items; 
            var customer = _customerService.GetCustomerInformationById(order.CustomerId);
            int paymentId = (int)(order.PaymentId == null ? 0 : order.PaymentId);
            ViewBag.CustomerInformation = customer.Information;
            var paymentNumer = _paymentService.GetPaymentById(paymentId);
            if(paymentNumer != null)
            {
                ViewBag.PaymentNumber = paymentNumer.Number;
            }
            else
            {
                ViewBag.PaymentNumber = "";
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            order.UserId = userId;
            return View(order);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult EditOrder(NewOrderVm model)
        {
            model.Cost = Convert.ToDecimal(model.CostToConvert);

            if (model.AcceptedRefund)
            {
                model.ChangedRefund = _refundService.SameReasonNotExists(model.ReasonRefund);
                if(model.ChangedRefund)
                {
                    model.RefundDate = System.DateTime.Now;
                    var refund = new RefundVm()
                    {
                        Reason = model.ReasonRefund,
                        CustomerId = model.CustomerId,
                        Accepted = model.AcceptedRefund,
                        OnWarranty = model.OnWarranty,
                        RefundDate = model.RefundDate,
                        OrderId = model.Id
                    };
                    var refundId = _refundService.AddRefund(refund);
                    model.RefundId = refundId;
                    if (model.OrderItems.Count > 0)
                    {
                        model.OrderItems.ForEach(oi =>
                        {
                            oi.RefundId = refundId;
                        });
                    }
                }
            }

            if (model.OrderItems.Count > 0)
            {
                model.OrderItems.ForEach(oi =>
                {
                    oi.UserId = model.UserId;
                });
                _orderService.UpdateOrder(model.AsOrderDto());
            }

            UseCouponIfEntered(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        public IActionResult ViewOrderDetails(int id)
        {
            var order = _orderService.GetOrderDetail(id);
            if (order is null)
            {
                return NotFound();
            }
            return View(order);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        public IActionResult DeleteOrder(int id)
        {
            _orderService.DeleteOrder(id);
            return Json("deleted");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        public IActionResult ShowMyOrders()
        {
            var userId = GetUserId();
            var orders = _orderService.GetAllOrdersByUserId(userId, 20, 1);
            return View(orders);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult ShowMyOrders(int pageSize, int pageNo)
        {
            var userId = GetUserId();
            var orders = _orderService.GetAllOrdersByUserId(userId, pageSize, pageNo);
            return View(orders);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult ShowOrdersPaid()
        {
            var model = _orderService.GetAllOrdersPaid(20, 1, "");

            return View(model);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost]
        public IActionResult ShowOrdersPaid(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var model = _orderService.GetAllOrdersPaid(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPatch]
        public IActionResult DispatchOrder(int id)
        {
            _orderService.DispatchOrder(id);
            return Ok();
        }

        private void UseCouponIfEntered(NewOrderVm model)
        {
            var id = _couponService.CheckPromoCode(model.RefCode);
            if (id != 0)
            {
                _orderService.AddCouponToOrder(id, model);
            }
        }
    }
}
