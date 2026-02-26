using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Supporting.TimeManagement;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.TimeManagement.Services
{
    internal sealed class JobManagementService : IJobManagementService
    {
        private readonly IEnumerable<IScheduleConfig> _configs;
        private readonly IScheduledJobRepository _scheduledJobRepo;
        private readonly IJobExecutionRepository _executionRepo;
        private readonly IJobStatusMonitor _monitor;

        public JobManagementService(
            IEnumerable<IScheduleConfig> configs,
            IScheduledJobRepository scheduledJobRepo,
            IJobExecutionRepository executionRepo,
            IJobStatusMonitor monitor)
        {
            _configs = configs;
            _scheduledJobRepo = scheduledJobRepo;
            _executionRepo = executionRepo;
            _monitor = monitor;
        }

        public async Task<IReadOnlyList<JobStatusSummary>> GetAllJobsAsync(CancellationToken ct = default)
        {
            var dbJobs = await _scheduledJobRepo.GetAllAsync(ct);
            var dbJobsDict = dbJobs.ToDictionary(j => j.Name.Value);

            var result = new List<JobStatusSummary>();
            foreach (var config in _configs)
            {
                dbJobsDict.TryGetValue(config.JobName, out var dbJob);
                var latest = _monitor.GetLatest(config.JobName);

                result.Add(new JobStatusSummary
                {
                    JobName = config.JobName,
                    JobType = config.JobType,
                    CronExpression = config.CronExpression,
                    IsEnabled = dbJob?.IsEnabled ?? true,
                    LastRunAt = dbJob?.LastRunAt ?? latest?.CompletedAt,
                    NextRunAt = dbJob?.NextRunAt,
                    LastSucceeded = latest?.Succeeded,
                    LastMessage = latest?.Message,
                    LastExecutionId = latest?.ExecutionId,
                    NeverRun = dbJob?.LastRunAt == null && latest == null
                });
            }

            return result;
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
                Source = (JobTriggerSource)e.Source
            }).ToList();
        }

        public async Task EnableAsync(string jobName, CancellationToken ct = default)
        {
            var job = await _scheduledJobRepo.GetByNameAsync(jobName, ct);
            if (job == null)
            {
                var config = _configs.FirstOrDefault(c => c.JobName == jobName)
                    ?? throw new BusinessException($"Job '{jobName}' not found in configuration.");
                job = ScheduledJob.Create(jobName, config.JobType, config.CronExpression, config.TimeZoneId, config.MaxRetries);
                await _scheduledJobRepo.AddAsync(job, ct);
                return;
            }

            job.Enable();
            await _scheduledJobRepo.UpdateAsync(job, ct);
        }

        public async Task DisableAsync(string jobName, CancellationToken ct = default)
        {
            var job = await _scheduledJobRepo.GetByNameAsync(jobName, ct);
            if (job == null)
            {
                var config = _configs.FirstOrDefault(c => c.JobName == jobName)
                    ?? throw new BusinessException($"Job '{jobName}' not found in configuration.");
                job = ScheduledJob.Create(jobName, config.JobType, config.CronExpression, config.TimeZoneId, config.MaxRetries);
                job.Disable();
                await _scheduledJobRepo.AddAsync(job, ct);
                return;
            }

            job.Disable();
            await _scheduledJobRepo.UpdateAsync(job, ct);
        }
    }
}
