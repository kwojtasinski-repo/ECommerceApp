using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Web.Models;
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

        [HttpGet]
        public IActionResult Index()
        {
            var model = _orderService.GetAllOrders(20, 1, "");
            return View(model);
        }

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

        [HttpGet]
        public IActionResult AddOrder()
        {
            var orderDate = System.DateTime.Now;
            ViewBag.Date = orderDate;
            var customers = _orderService.GetAllCustomers().ToList();
            ViewBag.Customers = customers;
            return View(new NewOrderVm());
        }

        [HttpPost]
        public IActionResult AddOrder(NewOrderVm model)
        {
            var id = _orderService.AddOrder(model);
            return RedirectToAction("AddOrderDetails", new { orderId = id });
        }

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

        [HttpPost]
        public IActionResult AddOrderDetails(NewOrderVm model)
        {
            var id = _orderService.CheckPromoCode(model.RefCode);
            if (id != 0)
            {
                _orderService.UpdateCoupon(id, model.Id);
            }

            if (model.OrderItems.Count > 1)
            {
                _orderService.AddOrderItems(model.OrderItems);

            }
            else
            {
                DeleteOrder(model.Id);
            }

            return RedirectToAction("View");
        }

        public IActionResult DeleteOrder(int id)
        {
            _orderService.DeleteOrder(id);
            return RedirectToAction("Index");
        }
    }
}
