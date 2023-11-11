using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services.Addresses;
using ECommerceApp.Application.Services.Authentication;
using ECommerceApp.Application.Services.Brands;
using ECommerceApp.Application.Services.ContactDetails;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Application.Services.Currencies;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.Services
{
    internal static class Extensions
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddTransient<IAddressService, AddressService>();
            services.AddTransient<IBrandService, BrandService>();
            services.AddTransient<IContactDetailService, ContactDetailService>();
            services.AddTransient<IContactDetailTypeService, ContactDetailTypeService>();
            services.AddTransient<ICouponService, CouponService>();
            services.AddTransient<ICouponTypeService, CouponTypeService>();
            services.AddTransient<ICouponUsedService, CouponUsedService>();
            services.AddTransient<ICurrencyService, CurrencyService>();
            services.AddTransient<ICurrencyRateService, CurrencyRateService>();
            services.AddHttpContextAccessor();
            return services;
        }
    }
}
