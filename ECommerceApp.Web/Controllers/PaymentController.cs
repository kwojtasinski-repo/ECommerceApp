using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.ViewModels.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Authorize]
    public class PaymentController : BaseController
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

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet]
        public IActionResult Index()
        {
            var model = _paymentService.GetPayments(20, 1, "");
            return View(model);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            if (!pageNo.HasValue)
            {
                pageNo = 1;
            }

            searchString ??= string.Empty;
            var model = _paymentService.GetPayments(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]       
        public IActionResult AddPayment(int id)
        {
            var payment = _paymentService.InitPayment(id);
            var currencies = _currencyService.GetAll();
            ViewBag.Currencies = currencies;
            return View(payment);
        }

        [HttpPost]
        public IActionResult AddPayment(PaymentVm model)
        {
            _paymentService.PaidIssuedPayment(model);
            return RedirectToAction("Index", "Item");
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet] 
        public IActionResult EditPayment(int id)
        {
            var payment = _paymentService.GetPaymentById(id);
            if (payment is null)
            {
                return NotFound();
            }
            var currencies = _currencyService.GetAll();
            ViewBag.Currencies = currencies;
            return View(payment);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost] 
        public IActionResult EditPayment(PaymentVm model)
        {
            _paymentService.UpdatePayment(model);
            return RedirectToAction("Index");
        }

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

        [Authorize(Roles = $"{MaintenanceRole}")]
        public IActionResult DeletePayment(int id)
        {
            return _paymentService.DeletePayment(id) 
                ? Json("deleted")
                : NotFound();
        }

        public IActionResult ViewMyPayments()
        {
            var userId = GetUserId();
            var payments = _paymentService.GetUserPayments(userId);
            return View(payments);
        }
    }
}
