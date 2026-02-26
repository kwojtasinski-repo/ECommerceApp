using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Infrastructure.Supporting.TimeManagement.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal static class Extensions
    {
        public static IServiceCollection AddTimeManagementInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<TimeManagementBuilder> configure)
        {
            var builder = new TimeManagementBuilder();
            configure(builder);

            foreach (var config in builder.Configs)
                services.AddSingleton<IScheduleConfig>(config);

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

            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CronSchedulerService>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DeferredJobPollerService>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, JobDispatcherService>());

            return services;
        }
    }
}
