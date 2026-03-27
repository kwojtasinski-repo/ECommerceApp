using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers
{
    [Authorize(Roles = BaseController.MaintenanceRole)]
    [Route("api/v2/jobs")]
    public class JobsController : BaseController
    {
        private readonly IJobManagementService _jobs;

        public JobsController(IJobManagementService jobs)
        {
            _jobs = jobs;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct = default)
        {
            var list = await _jobs.GetAllJobsAsync(ct);
            return Ok(list);
        }

        [HttpGet("{jobName}/history")]
        public async Task<IActionResult> GetHistory(
            string jobName,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var list = await _jobs.GetHistoryAsync(jobName, page, pageSize, ct);
            return Ok(list);
        }

        [HttpGet("deferred")]
        public async Task<IActionResult> GetDeferredQueue(CancellationToken ct = default)
        {
            var list = await _jobs.GetDeferredQueueAsync(ct);
            return Ok(list);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterJobVm vm, CancellationToken ct = default)
        {
            await _jobs.RegisterAsync(vm, ct);
            return Ok();
        }

        [HttpPut("{jobName}/enable")]
        public async Task<IActionResult> Enable(string jobName, CancellationToken ct = default)
        {
            await _jobs.EnableAsync(jobName, ct);
            return Ok();
        }

        [HttpPut("{jobName}/disable")]
        public async Task<IActionResult> Disable(string jobName, CancellationToken ct = default)
        {
            await _jobs.DisableAsync(jobName, ct);
            return Ok();
        }
    }
}
