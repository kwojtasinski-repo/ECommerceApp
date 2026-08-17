using ECommerceApp.Application.Sales.Payments.DTOs;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Application.Sales.Payments.ViewModels;
using ECommerceApp.Domain.Sales.Payments;
using ECommerceApp.Web.Areas.Presale.Authorization;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Sales.Controllers
{
    [Area("Sales")]
    [Authorize(Policy = "CustomerOrGuest")]
    public class PaymentsController : BaseController
    {
        private readonly IPaymentService _paymentService;
        private readonly IOrderAccessAuthorizer _orderAccessAuthorizer;

        public PaymentsController(IPaymentService paymentService, IOrderAccessAuthorizer orderAccessAuthorizer)
        {
            _paymentService = paymentService;
            _orderAccessAuthorizer = orderAccessAuthorizer;
        }

        // Paged admin list — IPaymentService.GetAllAsync is not yet implemented.
        // Stub: returns an empty list until the service method is added.
        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public IActionResult Index()
        {
            return View(new PaymentListVm(Array.Empty<PaymentVm>(), 1, 20, 0));
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        public IActionResult Index(int pageSize, int? pageNo, string searchString)
        {
            return View(new PaymentListVm(Array.Empty<PaymentVm>(), pageNo ?? 1, pageSize, 0));
        }

        [HttpGet]
        public async Task<IActionResult> Create(int id)
        {
            var payment = await _paymentService.GetByOrderIdAsync(id);

            if (payment is null)
                return NotFound();
            if (!await _orderAccessAuthorizer.AuthorizeAsync(
                    User,
                    new OrderAccessResource(payment.OrderId, payment.UserId),
                    HttpContext.RequestAborted))
                return OrderAccessDenial.Result(this, User, payment.OrderId);
            if (!string.Equals(payment.Status, PaymentStatus.Pending.ToString(), StringComparison.Ordinal))
                return NotFound();
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ConfirmPaymentDto dto)
        {
            var preCheckPayment = await _paymentService.GetByIdAsync(dto.PaymentId);
            if (preCheckPayment is null)
                return NotFound();
            if (!await _orderAccessAuthorizer.AuthorizeAsync(
                    User,
                    new OrderAccessResource(preCheckPayment.OrderId, preCheckPayment.UserId),
                    HttpContext.RequestAborted))
                return OrderAccessDenial.Result(this, User, preCheckPayment.OrderId);

            var result = await _paymentService.ConfirmAsync(dto);
            var payment = await _paymentService.GetByIdAsync(dto.PaymentId);
            if (payment is null)
                return NotFound();

            if (result == PaymentOperationResult.Success)
            {
                return RedirectToAction("Details", "Orders", new { area = "Sales", id = payment.OrderId });
            }

            ModelState.AddModelError(string.Empty, result switch
            {
                PaymentOperationResult.AlreadyConfirmed  => "Płatność została już potwierdzona.",
                PaymentOperationResult.AlreadyExpired    => "Płatność wygasła i nie może zostać potwierdzona.",
                PaymentOperationResult.AlreadyRefunded   => "Płatność została już zwrócona.",
                PaymentOperationResult.AlreadyCancelled  => "Płatność została anulowana i nie może zostać potwierdzona.",
                _                                        => "Nie udało się potwierdzić płatności."
            });
            return View(payment);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var payment = await _paymentService.GetByIdAsync(id);
            if (payment is null)
                return NotFound();
            if (!await _orderAccessAuthorizer.AuthorizeAsync(
                    User,
                    new OrderAccessResource(payment.OrderId, payment.UserId),
                    HttpContext.RequestAborted))
                return OrderAccessDenial.Result(this, User, payment.OrderId);
            return View(payment);
        }

        // User payments list
        [HttpGet]
        public async Task<IActionResult> MyPayments()
        {
            var payments = await _paymentService.GetByUserIdAsync(GetUserId());
            payments = GuestAccessScope.ScopeToCurrentOrder(User, payments, p => p.OrderId);
            return View(payments);
        }
    }
}
