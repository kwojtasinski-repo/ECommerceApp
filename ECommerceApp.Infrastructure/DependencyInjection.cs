using ECommerceApp.Domain.Interface;
using ECommerceApp.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
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
            return services;
        }
    }
}
