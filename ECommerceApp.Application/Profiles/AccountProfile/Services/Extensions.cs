using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Profiles.AccountProfile.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddAccountProfileServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IAccountProfileService, AccountProfileService>()
                .AddScoped<IAccountAddressService, AccountAddressService>()
                .AddScoped<IAccountContactDetailService, AccountContactDetailService>()
                .AddScoped<IAccountContactDetailTypeService, AccountContactDetailTypeService>();
        }
    }
}
