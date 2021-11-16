﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.Refund;
using ECommerceApp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.Application.ViewModels.OrderItem;

namespace ECommerceApp.Web.Controllers
{
    public class OrderController : Controller
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult Index()
        {
            var model = _orderService.GetAllOrders(20, 1, "");
            return View(model);
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

            var model = _orderService.GetAllOrders(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult AddOrder()
        {
            Random random = new Random();
            var orderDate = System.DateTime.Now;
            ViewBag.Date = orderDate;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
            {
                //var customers = _orderService.GetAllCustomers().ToList();
                var customers = _customerService.GetCustomersInformationByUserId(userId).ToList();
                ViewBag.Customers = customers;
                var order = new OrderVm() { Number = random.Next(100, 10000), };
                order.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return View(order);
            }
            else
            {
                return Redirect("~/Identity/Account/Login");
            }
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddOrder(OrderVm model)
        {
            var id = _orderService.AddOrder(model);
            return RedirectToAction("AddOrderDetails", new { orderId = id });
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult AddOrderToCart()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Redirect("~/Identity/Account/Register");
            }
            var orderItems = new NewOrderItemVm();
            var items = _itemService.GetItemsAddToCart();
            orderItems.Items = items;
            orderItems.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.ItemsJson = Json(items);
            return View(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddOrderToCart(NewOrderItemVm model)
        {
            var id = _orderItemService.AddOrderItem(model.AsOrderItemVm());
            return RedirectToAction("Index");
        }

        
        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult OrderRealization()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Redirect("~/Identity/Account/Register");
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orderItems = _orderItemService.GetOrderItemsForRealization(oi => oi.UserId == userId && oi.OrderId == null).ToList();
            Random random = new Random();
            var orderDate = System.DateTime.Now;
            ViewBag.Date = orderDate;
            var customers = _customerService.GetCustomersInformationByUserId(userId).ToList();
            ViewBag.Customers = customers;
            var order = new NewOrderVm() { Number = random.Next(100, 10000), OrderItems = orderItems, UserId = userId };
            return View(order);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult OrderRealization(NewOrderVm model)
        {
            int orderId;
            if (model.CustomerData)
            {
                orderId = _orderService.AddOrder(model.AsOrderVm());
            }
            else
            {
                var customerId = _customerService.AddCustomer(model.NewCustomer);
                model.CustomerId = customerId;
                orderId = _orderService.AddOrder(model.AsOrderVm());
            }
            return RedirectToAction("AddOrderSummary", new { id = orderId });
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult AddOrderDetails(int orderId)
        {
            var order = _orderService.GetOrderForRealization(orderId);
            var customer = _customerService.GetCustomerInformationById(order.CustomerId);
            order.Items = _itemService.GetAllItems(i => true).ToList();
            ViewBag.CustomerInformation = customer.Information;
            return View(order);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
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
                _orderService.UpdateOrder(model.AsOrderVm());
            }
            else
            {
                DeleteOrder(model.Id);
            }

            return RedirectToAction("AddOrderSummary", new { id = model.Id });
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult AddOrderSummary(int id)
        {
            var order = _orderService.GetOrderForRealization(id);
            return View(order);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult ShowMyCart()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orderItems = _orderItemService.GetOrderItemsNotOrderedByUserId(userId, 20, 1);
            return View(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult ShowOrdersByCustomerId(int customerId)
        {
            var orderItems = _orderService.GetAllOrdersByCustomerId(customerId, 20, 1);
            ViewBag.InputParameterId = customerId;
            return View(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
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

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult EditOrder(NewOrderVm model)
        {
            model.Cost = Convert.ToDecimal(model.CostToConvert);
            UseCouponIfEntered(model);

            if (model.AcceptedRefund)
            {
                model.ChangedRefund = _refundService.CheckEnteredRefund(model.ReasonRefund);
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
                _orderService.UpdateOrder(model.AsOrderVm());
            }
            
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ViewOrderDetails(int id)
        {
            var order = _orderService.GetOrderDetail(id);
            if (order is null)
            {
                return NotFound();
            }
            return View(order);
        }
        
        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult DeleteOrder(int id)
        {
            _orderService.DeleteOrder(id);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ShowMyOrders()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orders = _orderService.GetAllOrdersByUserId(userId, 20, 1);
            return View(orders);
        }

        private void UseCouponIfEntered(NewOrderVm model)
        {
            var id = _couponService.CheckPromoCode(model.RefCode);
            if (id != 0)
            {
                var couponUsedId = _orderService.AddCouponToOrder(id, model);
                model.CouponUsedId = couponUsedId;
                if (model.OrderItems.Count > 0)
                {
                    model.OrderItems.ForEach(oi =>
                    {
                        oi.CouponUsedId = couponUsedId;
                    });
                }
            }

        }
    }
}
