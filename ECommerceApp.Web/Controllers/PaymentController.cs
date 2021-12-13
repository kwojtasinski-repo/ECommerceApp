using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly ICurrencyService _currencyService;

        public PaymentController(IOrderService orderService, IPaymentService paymentService, ICurrencyService currencyService)
        {
            _orderService = orderService;
            _paymentService = paymentService;
            _currencyService = currencyService;
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet]
        public IActionResult Index()
        {
            var model = _paymentService.GetPayments(20, 1, "");
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

            var model = _paymentService.GetPayments(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpGet]       
        public IActionResult AddPayment(int id)
        {
            var payment = _paymentService.InitPayment(id);
            var currencies = _currencyService.GetAll(cr => true);
            ViewBag.Currencies = currencies;
            return View(payment);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        [HttpPost]
        public IActionResult AddPayment(PaymentVm model)
        {
            var id = _paymentService.AddPayment(model);
            return RedirectToAction("Index", "Item");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpGet] 
        public IActionResult EditPayment(int id)
        {
            var payment = _paymentService.GetPaymentById(id);
            if (payment is null)
            {
                return NotFound();
            }
            return View(payment);
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service")]
        [HttpPost] 
        public IActionResult EditPayment(PaymentVm model)
        {
            _paymentService.UpdatePayment(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administratorm, Admin, Manager, Service, User")]
        [HttpGet]
        public IActionResult ViewPayment(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var payment = _paymentService.GetPaymentDetails(id, userId);
            if (payment is null)
            {
                return NotFound("Nie znaleziono płatności");
            }
            return View(payment);
        }

        [Authorize(Roles = "Administrator, Admin, Manager")]
        public IActionResult DeletePayment(int id)
        {
            _paymentService.DeletePayment(id);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrator, Admin, Manager, Service, User")]
        public IActionResult ViewMyPayments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var payments = _paymentService.GetPaymentsForUser(p => true, userId);
            return View(payments);
        }
    }
}
