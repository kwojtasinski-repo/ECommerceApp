using ECommerceApp.Application.Sales.Payments.DTOs;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Application.Sales.Payments.ViewModels;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Sales.Controllers
{
    [Area("Sales")]
    [Authorize]
    public class PaymentsController : BaseController
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
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
        public IActionResult Index(int pageSize, int? pageNo, string? searchString)
        {
            return View(new PaymentListVm(Array.Empty<PaymentVm>(), pageNo ?? 1, pageSize, 0));
        }

        [HttpGet]
        public async Task<IActionResult> Create(int id)
        {
            var payment = await _paymentService.GetPendingByOrderIdAsync(id, GetUserId());
            if (payment is null)
                return NotFound();
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ConfirmPaymentDto dto)
        {
            var result = await _paymentService.ConfirmAsync(dto);
            if (result == PaymentOperationResult.Success)
                return RedirectToAction(nameof(MyPayments));

            var payment = await _paymentService.GetByIdAsync(dto.PaymentId);
            if (payment is null)
                return NotFound();

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
            if (!MaintenanceRoles.Any(r => User.IsInRole(r)) && payment.UserId != GetUserId())
                return Forbid();
            return View(payment);
        }

        // User payments list
        [HttpGet]
        public async Task<IActionResult> MyPayments()
        {
            var payments = await _paymentService.GetByUserIdAsync(GetUserId());
            return View(payments);
        }
    }
}
