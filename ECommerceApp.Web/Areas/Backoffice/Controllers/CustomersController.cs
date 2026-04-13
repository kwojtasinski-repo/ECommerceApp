using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Backoffice.Controllers
{
    [Area("Backoffice")]
    [Authorize(Roles = ManagingRole)]
    public class CustomersController : BaseController
    {
        private readonly IBackofficeCustomerService _service;

        public CustomersController(IBackofficeCustomerService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _service.GetCustomersAsync(20, 1, null);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo, string? searchString)
        {
            pageNo ??= 1;
            var model = await _service.GetCustomersAsync(pageSize, pageNo.Value, searchString);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _service.GetCustomerDetailAsync(id);
            if (model is null)
                return NotFound();
            return View(model);
        }
    }
}
