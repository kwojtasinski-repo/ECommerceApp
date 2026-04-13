using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Backoffice.Controllers
{
    [Area("Backoffice")]
    [Authorize(Roles = ManagingRole)]
    public class PaymentsController : BaseController
    {
        private readonly IBackofficePaymentService _service;

        public PaymentsController(IBackofficePaymentService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _service.GetPaymentsAsync(20, 1);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo)
        {
            pageNo ??= 1;
            var model = await _service.GetPaymentsAsync(pageSize, pageNo.Value);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Unpaid()
        {
            var model = await _service.GetUnpaidOrderPaymentsAsync(20, 1);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Unpaid(int pageSize, int? pageNo)
        {
            pageNo ??= 1;
            var model = await _service.GetUnpaidOrderPaymentsAsync(pageSize, pageNo.Value);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _service.GetPaymentDetailAsync(id);
            if (model is null)
                return NotFound();
            return View(model);
        }
    }
}
