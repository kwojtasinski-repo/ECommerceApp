using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Backoffice.Controllers
{
    [Area("Backoffice")]
    [Authorize(Roles = ManagingRole)]
    public class RefundsController : BaseController
    {
        private readonly IBackofficeRefundService _service;

        public RefundsController(IBackofficeRefundService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _service.GetRefundsAsync(20, 1);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo)
        {
            pageNo ??= 1;
            var model = await _service.GetRefundsAsync(pageSize, pageNo.Value);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _service.GetRefundDetailAsync(id);
            if (model is null)
                return NotFound();
            return View(model);
        }
    }
}
