using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Currencies;
using ECommerceApp.Application.Services.Orders;
using ECommerceApp.Application.Services.Payments;
using ECommerceApp.Application.ViewModels.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

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
            try
            {
                _paymentService.PaidIssuedPayment(model);
                return RedirectToAction("ViewMyPayments");
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction("ViewMyPayments", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpGet] 
        public IActionResult EditPayment(int id)
        {
            var payment = _paymentService.GetPaymentById(id);
            if (payment is null)
            {
                var errorModel = BuildErrorModel("paymentNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new PaymentVm { });
            }
            var currencies = _currencyService.GetAll();
            ViewBag.Currencies = currencies;
            return View(payment);
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        [HttpPost] 
        public IActionResult EditPayment(PaymentVm model)
        {
            try
            {
                _paymentService.UpdatePayment(model);
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                var errorModel = BuildErrorModel(ex.ErrorCode, ex.Arguments);
                return RedirectToAction("Index", new { Error = errorModel.ErrorCode, Params = errorModel.GenerateParamsString() });
            }
        }

        [HttpGet]
        public IActionResult ViewPayment(int id)
        {
            var payment = _paymentService.GetPaymentDetails(id);
            if (payment is null)
            {
                var errorModel = BuildErrorModel("paymentNotFound", new Dictionary<string, string> { { "id", $"{id}" } });
                HttpContext.Request.Query = errorModel.AsQueryCollection();
                return View(new PaymentDetailsVm { Payment = new Application.DTO.PaymentDetailsDto() });
            }
            return View(new PaymentDetailsVm { Payment = payment });
        }

        [Authorize(Roles = $"{MaintenanceRole}")]
        public IActionResult DeletePayment(int id)
        {
            try
            {
                return _paymentService.DeletePayment(id)
                    ? Json("deleted")
                    : NotFound();
            }
            catch (BusinessException exception)
            {
                return BadRequest(MapExceptionToResponseStatus(exception));
            }
        }

        public IActionResult ViewMyPayments()
        {
            var userId = GetUserId();
            var payments = _paymentService.GetUserPayments(userId);
            return View(payments);
        }
    }
}
