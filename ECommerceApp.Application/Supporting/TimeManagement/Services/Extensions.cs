using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Supporting.TimeManagement.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddTimeManagementServices(this IServiceCollection services)
        {
            return services.AddScoped<IJobManagementService, JobManagementService>();
        }
    }
}
