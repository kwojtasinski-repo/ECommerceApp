using ECommerceApp.Application.Sales.Payments.DTOs;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Application.Sales.Payments.ViewModels;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
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

        // id = orderId — loads the pending payment for an order
        [HttpGet]
        public async Task<IActionResult> Create(int id)
        {
            var payment = await _paymentService.GetByOrderIdAsync(id);
            if (payment is null)
                return NotFound();
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ConfirmPaymentDto dto)
        {
            await _paymentService.ConfirmAsync(dto);
            return RedirectToAction(nameof(MyPayments));
        }

        // IPaymentService has no update method — Edit shows current details only.
        [Authorize(Roles = MaintenanceRole)]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var payment = await _paymentService.GetByIdAsync(id);
            if (payment is null)
                return NotFound();
            return View(payment);
        }

        [Authorize(Roles = MaintenanceRole)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id)
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var payment = await _paymentService.GetByIdAsync(id);
            if (payment is null)
                return NotFound();
            return View(payment);
        }

        // User payments list — IPaymentService.GetByUserIdAsync is not yet implemented.
        // Stub: returns an empty list until the service method is added.
        [HttpGet]
        public IActionResult MyPayments()
        {
            return View(Array.Empty<PaymentVm>());
        }
    }
}
