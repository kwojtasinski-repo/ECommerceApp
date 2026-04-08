using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class TimeManagementDbContext : DbContext, ITimeManagementDbContext
    {
        public DbSet<ScheduledJob> ScheduledJobs => Set<ScheduledJob>();
        public DbSet<DeferredJobInstance> DeferredJobQueue => Set<DeferredJobInstance>();
        public DbSet<JobExecution> JobExecutions => Set<JobExecution>();

        public TimeManagementDbContext(DbContextOptions<TimeManagementDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema(TimeManagementConstants.SchemaName);
            builder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Supporting.TimeManagement.Configurations"));
            builder.UseUtcDateTimes();
        }
    }
}
