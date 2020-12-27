using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
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
                var customers = _orderService.GetCustomersByUserId(userId).ToList();
                ViewBag.Customers = customers;
                var order = new NewOrderVm() { Number = random.Next(100, 10000), };
                return View(order);
            }
            else
            {
                return Redirect("~/Identity/Account/Login");
            }
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddOrder(NewOrderVm model)
        {
            var id = _orderService.AddOrder(model);
            return RedirectToAction("AddOrderDetails", new { orderId = id });
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult AddOrderDetails(int orderId)
        {
            var order = _orderService.GetOrderById(orderId);
            var items = _orderService.GetAllItemsToOrder().ToList();
            order.Items = items;
            var customer = _orderService.GetCustomerById(order.CustomerId);
            ViewBag.CustomerInformation = customer.Information;
            return View(order);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddOrderDetails(NewOrderVm model)
        {
            var id = _orderService.CheckPromoCode(model.RefCode);
            model.Cost = Convert.ToDecimal(model.CostToConvert);
            if (id != 0)
            {
                var couponUsedId = _orderService.UpdateCoupon(id, model);
                model.CouponUsedId = couponUsedId;
                if (model.OrderItems.Count > 0)
                {
                    model.OrderItems.ForEach(oi =>
                    {
                        oi.CouponUsedId = couponUsedId;
                    });
                }
            }

            if (model.OrderItems.Count > 0)
            {
                _orderService.UpdateOrder(model);
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
            var order = _orderService.GetOrderById(id);
            return View(order);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]       
        public IActionResult Payment(int id)
        {
            Random random = new Random();
            var order = _orderService.GetOrderById(id);
            var customer = _orderService.GetCustomerById(order.CustomerId);
            var payment = new NewPaymentVm()
            {
                OrderId = order.Id,
                Number = random.Next(0, 1000),
                DateOfOrderPayment = System.DateTime.Now,
                CustomerId = order.CustomerId,
                OrderNumber = order.Number,
                CustomerName = customer.Information,
                OrderCost = order.Cost
            };
            return View(payment);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult Payment(NewPaymentVm model)
        {
            var id = _orderService.AddPayment(model);
            return RedirectToAction("Index", "Item");
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
        [HttpGet]
        public IActionResult ShowPayments()
        {
            var payments = _orderService.GetAllPayments(20, 1, "");
            return View(payments);
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpPost]
        public IActionResult ShowPayments(int pageSize, int? pageNo, string searchString)
        {
            if(!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var orderItems = _orderService.GetAllPayments(pageSize, pageNo.Value, searchString);
            return View(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult ShowRefunds()
        {
            var refunds = _orderService.GetAllRefunds(20, 1, "");
            return View(refunds);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost]
        public IActionResult ShowRefunds(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var refunds = _orderService.GetAllRefunds(pageSize, pageNo.Value, searchString);
            return View(refunds);
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpGet]
        public IActionResult ShowAllOrderItems()
        {
            var orderItems = _orderService.GetAllItemsOrdered(20, 1, "");
            return View(orderItems);
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        [HttpPost]
        public IActionResult ShowAllOrderItems(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            if (searchString is null)
            {
                searchString = String.Empty;
            }

            var orderItems = _orderService.GetAllItemsOrdered(pageSize, pageNo.Value, searchString);
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
            var order = _orderService.GetOrderById(id);
            var items = _orderService.GetAllItemsToOrder().ToList();
            order.Items = items;
            var customer = _orderService.GetCustomerById(order.CustomerId);
            int paymentId = (int)(order.PaymentId == null ? 0 : order.PaymentId);
            ViewBag.CustomerInformation = customer.Information;
            var paymentNumer = _orderService.GetPaymentById(paymentId);
            if(paymentNumer != null)
            {
                ViewBag.PaymentNumber = paymentNumer.Number;
            }
            else
            {
                ViewBag.PaymentNumber = "";
            }
            return View(order);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult EditOrder(NewOrderVm model)
        {

            var id = _orderService.CheckPromoCode(model.RefCode);
            model.Cost = Convert.ToDecimal(model.CostToConvert);

            if (id != 0)
            {
                model.ChangedCode = true;
                var couponUsedId = _orderService.UpdateCoupon(id, model);
                model.CouponUsedId = couponUsedId;
                if (model.OrderItems.Count > 0)
                {
                    model.OrderItems.ForEach(oi =>
                    {
                        oi.CouponUsedId = couponUsedId;
                    });
                }
            }

            if (model.AcceptedRefund)
            {
                model.ChangedRefund = _orderService.CheckEnteredRefund(model.ReasonRefund);
                if(model.ChangedRefund)
                {
                    model.RefundDate = System.DateTime.Now;
                    var refund = new NewRefundVm()
                    {
                        Reason = model.ReasonRefund,
                        CustomerId = model.CustomerId,
                        Accepted = model.AcceptedRefund,
                        OnWarranty = model.OnWarranty,
                        RefundDate = model.RefundDate,
                        OrderId = model.Id
                    };
                    var refundId = _orderService.AddRefund(refund);
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
                _orderService.UpdateOrder(model);
            }
            
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet] 
        public IActionResult EditPayment(int id)
        {
            var payment = _orderService.GetPaymentForEdit(id);
            return View(payment);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost] 
        public IActionResult EditPayment(NewPaymentVm model)
        {
            _orderService.UpdatePayment(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ViewOrderDetails(int id)
        {
            var order = _orderService.GetOrderDetail(id);
            return View(order);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ViewPaymentDetails(int id)
        {
            var payment = _orderService.GetPaymentDetail(id);
            return View(payment);
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        public IActionResult ViewOrderItemDetails(int id)
        {
            var payment = _orderService.GetOrderItemDetail(id);
            return View(payment);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ViewRefundDetails(int id)
        {
            var refund = _orderService.GetRefundDetail(id);
            return View(refund);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult DeleteOrder(int id)
        {
            _orderService.DeleteOrder(id);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        public IActionResult DeleteRefund(int id)
        {
            _orderService.DeleteRefund(id);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        public IActionResult DeletePayment(int id)
        {
            _orderService.DeletePayment(id);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ShowMyOrders(string id)
        {
            var orders = _orderService.GetAllOrdersByUserId(id, 20, 1);
            return View(orders);
        }
    }
}
