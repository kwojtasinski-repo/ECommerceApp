using ECommerceApp.Application.Supporting.TimeManagement;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.AccountProfile.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddUserProfileServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IUserProfileService, UserProfileService>()
                .AddScoped<IGuestPromotionService, GuestPromotionService>()
                .AddScoped<IScheduledTask, UnclaimedGuestProfileCleanupTask>();
        }
    }
}
