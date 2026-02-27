using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Supporting.TimeManagement.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal static class Extensions
    {
        public static IServiceCollection AddTimeManagementInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<TimeManagementDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services
                .AddScoped<IScheduledJobRepository, ScheduledJobRepository>()
                .AddScoped<IDeferredJobInstanceRepository, DeferredJobInstanceRepository>()
                .AddScoped<IJobExecutionRepository, JobExecutionRepository>();

            services.AddSingleton<JobTriggerChannel>();
            services.AddSingleton<InMemoryJobStatusMonitor>();
            services.AddSingleton<IJobStatusMonitor>(sp => sp.GetRequiredService<InMemoryJobStatusMonitor>());

            services.AddScoped<IJobTrigger, JobTriggerService>();
            services.AddScoped<IDeferredJobScheduler, DeferredJobScheduler>();

            services.AddHostedService<CronSchedulerService>();
            services.AddHostedService<DeferredJobPollerService>();
            services.AddHostedService<JobDispatcherService>();

            services.AddScoped<IDbContextMigrator, DbContextMigrator<TimeManagementDbContext>>();

            return services;
        }
    }
}
