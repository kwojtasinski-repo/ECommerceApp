using ECommerceApp.Application.Backoffice.Services;
using ECommerceApp.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Backoffice.Controllers
{
    [Area("Backoffice")]
    [Authorize(Roles = ManagingRole)]
    public class JobsController : BaseController
    {
        private readonly IBackofficeJobService _service;

        public JobsController(IBackofficeJobService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _service.GetJobsAsync(20, 1);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(int pageSize, int? pageNo)
        {
            pageNo ??= 1;
            var model = await _service.GetJobsAsync(pageSize, pageNo.Value);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string name)
        {
            var model = await _service.GetJobDetailAsync(name);
            if (model is null)
                return NotFound();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var model = await _service.GetJobHistoryAsync(20, 1);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> History(int pageSize, int? pageNo)
        {
            pageNo ??= 1;
            var model = await _service.GetJobHistoryAsync(pageSize, pageNo.Value);
            return View(model);
        }
    }
}
