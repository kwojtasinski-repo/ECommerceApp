using ECommerceApp.Domain.Supporting.TimeManagement;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal interface ITimeManagementDbContext
    {
        DbSet<ScheduledJob> ScheduledJobs { get; }
        DbSet<DeferredJobInstance> DeferredJobQueue { get; }
        DbSet<JobExecution> JobExecutions { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
