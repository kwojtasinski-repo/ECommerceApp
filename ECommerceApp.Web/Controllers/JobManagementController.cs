using ECommerceApp.Application.Permissions;
using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Controllers
{
    [Authorize(Roles = UserPermissions.Roles.Administrator)]
    public class JobManagementController : BaseController
    {
        private readonly IJobManagementService _service;
        private readonly IJobTrigger _trigger;

        public JobManagementController(IJobManagementService service, IJobTrigger trigger)
        {
            _service = service;
            _trigger = trigger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var jobs = await _service.GetAllJobsAsync();
            return View(jobs);
        }

        [HttpGet]
        public async Task<IActionResult> History(string jobName, int page = 1)
        {
            const int pageSize = 20;
            var history = await _service.GetHistoryAsync(jobName, page, pageSize);
            ViewBag.JobName = jobName;
            ViewBag.Page = page;
            return View(history);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Trigger(string jobName)
        {
            await _trigger.TriggerAsync(jobName);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enable(string jobName)
        {
            await _service.EnableAsync(jobName);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable(string jobName)
        {
            await _service.DisableAsync(jobName);
            return RedirectToAction(nameof(Index));
        }
    }
}
