using System;
using System.Threading.Tasks;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers
{
    [Route("v2/jobs")]
    public class V2JobController : Controller
    {
        private readonly IJobManagementService _jobService;
        private readonly IJobTrigger _trigger;
        private readonly IDeferredJobScheduler _deferredScheduler;

        public V2JobController(IJobManagementService jobService, IJobTrigger trigger, IDeferredJobScheduler deferredScheduler)
        {
            _jobService = jobService;
            _trigger = trigger;
            _deferredScheduler = deferredScheduler;
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

        [HttpGet("register")]
        public IActionResult Register() => View(new RegisterJobVm { MaxRetries = 3 });

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterJobVm vm)
        {
            await _jobService.RegisterAsync(vm);
            TempData["Success"] = $"Job '{vm.JobName}' registered.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("deferred-queue")]
        public async Task<IActionResult> DeferredQueue() =>
            View(await _jobService.GetDeferredQueueAsync());

        [HttpGet("schedule-deferred")]
        public IActionResult ScheduleDeferred() => View(new ScheduleDeferredJobVm());

        [HttpPost("schedule-deferred")]
        public async Task<IActionResult> ScheduleDeferred(ScheduleDeferredJobVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);
            var runAt = DateTime.UtcNow.AddMinutes(vm.DelayMinutes);
            await _deferredScheduler.ScheduleAsync(vm.JobName, vm.EntityId, runAt);
            TempData["Success"] = $"Deferred job '{vm.JobName}' scheduled to run in {vm.DelayMinutes} min.";
            return RedirectToAction(nameof(Index));
        }
    }
}
