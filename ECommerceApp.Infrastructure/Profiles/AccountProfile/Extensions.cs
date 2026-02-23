using ECommerceApp.Domain.Profiles.AccountProfile;
using ECommerceApp.Infrastructure.Profiles.AccountProfile.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile
{
    internal static class Extensions
    {
        public static IServiceCollection AddAccountProfileInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AccountProfileDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            return services
                .AddScoped<IAccountProfileRepository, AccountProfileRepository>()
                .AddScoped<IAddressRepository, AddressRepository>()
                .AddScoped<IContactDetailRepository, ContactDetailRepository>()
                .AddScoped<IContactDetailTypeRepository, ContactDetailTypeRepository>();
        }
    }
}
