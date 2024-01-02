using System;
using System.Security.Claims;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Infrastructure.Permissions;
using Microsoft.AspNetCore.Authorization;
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpGet]
        public IActionResult Index()
        {
            var model = _paymentService.GetPayments(20, 1, "");
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

            var model = _paymentService.GetPayments(pageSize, pageNo.Value, searchString);

            return View(model);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]       
        public IActionResult AddPayment(int id)
        {
            var payment = _paymentService.InitPayment(id);
            var currencies = _currencyService.GetAll(cr => true);
            ViewBag.Currencies = currencies;
            return View(payment);
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpPost]
        public IActionResult AddPayment(PaymentVm model)
        {
            var id = _paymentService.PaidIssuedPayment(model);
            return RedirectToAction("Index", "Item");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
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

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}")]
        [HttpPost] 
        public IActionResult EditPayment(PaymentVm model)
        {
            _paymentService.UpdatePayment(model);
            return RedirectToAction("Index");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        [HttpGet]
        public IActionResult ViewPayment(int id)
        {
            var payment = _paymentService.GetPaymentDetails(id);
            if (payment is null)
            {
                return NotFound("Nie znaleziono płatności");
            }
            return View(new PaymentDetailsVm { Payment = payment });
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}")]
        public IActionResult DeletePayment(int id)
        {
            _paymentService.DeletePayment(id);
            return Json("deleted");
        }

        [Authorize(Roles = $"{UserPermissions.Roles.Administrator}, {UserPermissions.Roles.Manager}, {UserPermissions.Roles.Service}, {UserPermissions.Roles.User}")]
        public IActionResult ViewMyPayments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var payments = _paymentService.GetUserPayments(userId);
            return View(payments);
        }
    }
}
