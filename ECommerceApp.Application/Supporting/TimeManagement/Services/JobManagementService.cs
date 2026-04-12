using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Domain.Shared;
using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.TimeManagement.Services
{
    internal sealed class JobManagementService : IJobManagementService
    {
        private readonly IScheduledJobRepository _scheduledJobRepo;
        private readonly IJobExecutionRepository _executionRepo;
        private readonly IDeferredJobInstanceRepository _deferredRepo;
        private readonly IJobStatusMonitor _monitor;

        public JobManagementService(
            IScheduledJobRepository scheduledJobRepo,
            IJobExecutionRepository executionRepo,
            IDeferredJobInstanceRepository deferredRepo,
            IJobStatusMonitor monitor)
        {
            _scheduledJobRepo = scheduledJobRepo;
            _executionRepo = executionRepo;
            _deferredRepo = deferredRepo;
            _monitor = monitor;
        }

        public async Task<IReadOnlyList<JobStatusSummary>> GetAllJobsAsync(CancellationToken ct = default)
        {
            var dbJobs = await _scheduledJobRepo.GetAllAsync(ct);
            return dbJobs.Select(dbJob =>
            {
                var latest = _monitor.GetLatest(dbJob.Name.Value);
                return new JobStatusSummary
                {
                    JobName = dbJob.Name.Value,
                    Schedule = dbJob.Schedule.Value,
                    IsEnabled = dbJob.IsEnabled,
                    LastRunAt = dbJob.LastRunAt ?? latest?.CompletedAt,
                    NextRunAt = dbJob.NextRunAt,
                    LastSucceeded = latest?.Succeeded,
                    LastMessage = latest?.Message,
                    LastExecutionId = latest?.ExecutionId,
                    NeverRun = dbJob.LastRunAt == null && latest == null
                };
            }).ToList();
        }

        public async Task<IReadOnlyList<JobExecutionRecord>> GetHistoryAsync(string jobName, int page, int pageSize, CancellationToken ct = default)
        {
            var executions = await _executionRepo.GetPagedByJobNameAsync(jobName, page, pageSize, ct);
            return executions.Select(e => new JobExecutionRecord
            {
                JobName = jobName,
                ExecutionId = e.ExecutionId,
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt ?? e.StartedAt,
                Succeeded = e.Succeeded,
                Message = e.Message,
                Source = e.Source
            }).ToList();
        }

        public async Task<IReadOnlyList<JobExecutionRecord>> GetAllHistoryAsync(int page, int pageSize, CancellationToken ct = default)
        {
            var executions = await _executionRepo.GetPagedAsync(page, pageSize, ct);
            return executions.Select(e => new JobExecutionRecord
            {
                JobName = e.JobName.Value,
                ExecutionId = e.ExecutionId,
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt ?? e.StartedAt,
                Succeeded = e.Succeeded,
                Message = e.Message,
                Source = e.Source
            }).ToList();
        }

        public async Task<int> GetAllHistoryCountAsync(CancellationToken ct = default)
            => await _executionRepo.GetCountAsync(ct);

        public async Task<IReadOnlyList<DeferredJobQueueVm>> GetDeferredQueueAsync(CancellationToken ct = default)
        {
            var rows = await _deferredRepo.GetAllAsync(ct);
            return rows.Select(d => new DeferredJobQueueVm
            {
                Id = d.Id.Value,
                JobName = d.JobName.Value,
                EntityId = d.EntityId.Value,
                RunAt = d.RunAt,
                Status = d.Status,
                RetryCount = d.RetryCount,
                MaxRetries = d.MaxRetries,
                CreatedAt = d.CreatedAt,
                ErrorMessage = d.ErrorMessage
            }).ToList();
        }

        public async Task EnableAsync(string jobName, CancellationToken ct = default)
        {
            var job = await _scheduledJobRepo.GetByNameAsync(jobName, ct)
                ?? throw new BusinessException($"Job '{jobName}' not found.");
            job.Enable();
            await _scheduledJobRepo.UpdateAsync(job, ct);
        }

        public async Task DisableAsync(string jobName, CancellationToken ct = default)
        {
            var job = await _scheduledJobRepo.GetByNameAsync(jobName, ct)
                ?? throw new BusinessException($"Job '{jobName}' not found.");
            job.Disable();
            await _scheduledJobRepo.UpdateAsync(job, ct);
        }

        public async Task RegisterAsync(RegisterJobVm vm, CancellationToken ct = default)
        {
            var existing = await _scheduledJobRepo.GetByNameAsync(vm.JobName, ct);
            if (existing != null)
                throw new BusinessException($"Job '{vm.JobName}' is already registered.");

            try
            {
                var job = ScheduledJob.Create(vm.JobName, vm.Schedule, vm.TimeZoneId, vm.MaxRetries);
                await _scheduledJobRepo.AddAsync(job, ct);
            }
            catch (DomainException ex)
            {
                throw new BusinessException(ex.Message);
            }
        }
    }
}
