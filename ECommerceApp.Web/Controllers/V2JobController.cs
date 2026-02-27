using System.Threading.Tasks;
using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Route("v2/jobs")]
    public class V2JobController : Controller
    {
        private readonly IJobManagementService _jobService;
        private readonly IJobTrigger _trigger;

        public V2JobController(IJobManagementService jobService, IJobTrigger trigger)
        {
            _jobService = jobService;
            _trigger = trigger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index() =>
            View(await _jobService.GetAllJobsAsync());

        [HttpGet("{jobName}/history")]
        public async Task<IActionResult> History(string jobName, int page = 1, int pageSize = 20)
        {
            ViewBag.JobName = jobName;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            return View(await _jobService.GetHistoryAsync(jobName, page, pageSize));
        }

        [HttpPost("{jobName}/trigger")]
        public async Task<IActionResult> Trigger(string jobName)
        {
            await _trigger.TriggerAsync(jobName);
            TempData["Success"] = $"Job '{jobName}' triggered.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("{jobName}/enable")]
        public async Task<IActionResult> Enable(string jobName)
        {
            await _jobService.EnableAsync(jobName);
            TempData["Success"] = $"Job '{jobName}' enabled.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("{jobName}/disable")]
        public async Task<IActionResult> Disable(string jobName)
        {
            await _jobService.DisableAsync(jobName);
            TempData["Success"] = $"Job '{jobName}' disabled.";
            return RedirectToAction(nameof(Index));
        }
    }
}
