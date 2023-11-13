using ECommerceApp.Domain.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Repositories
{
    internal static class Extensions
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddTransient<ICustomerRepository, CustomerRepository>();
            services.AddTransient<IItemRepository, ItemRepository>();
            services.AddTransient<IOrderRepository, OrderRepository>();
            services.AddTransient<ICouponRepository, CouponRepository>();
            services.AddTransient(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddTransient<IImageRepository, ImageRepository>();
            services.AddTransient<ICouponTypeRepository, CouponTypeRepository>();
            services.AddTransient<ICouponUsedRepository, CouponUsedRepository>();
            services.AddTransient<IBrandRepository, BrandRepository>();
            services.AddTransient<IPaymentRepository, PaymentRepository>();
            services.AddTransient<IRefundRepository, RefundRepository>();
            services.AddTransient<ITagRepository, TagRepository>();
            services.AddTransient<ITypeRepository, TypeRepository>();
            services.AddTransient<IOrderItemRepository, OrderItemRepository>();
            services.AddTransient<IContactDetailRepository, ContactDetailRepository>();
            services.AddTransient<IContactDetailTypeRepository, ContactDetailTypeRepository>();
            services.AddTransient<IAddressRepository, AddressRepository>();
            services.AddTransient<ICurrencyRepository, CurrencyRepository>();
            services.AddTransient<ICurrencyRateRepository, CurrencyRateRepository>();
            return services;
        }
    }
}
