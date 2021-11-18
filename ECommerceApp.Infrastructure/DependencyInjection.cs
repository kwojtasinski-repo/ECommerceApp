﻿using ECommerceApp.Domain.Interface;
using ECommerceApp.Infrastructure.External.Client;
using ECommerceApp.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
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
            services.AddTransient<IOrderItemRepository, OrderItemRepository>();
            services.AddTransient<IContactDetailRepository, ContactDetailRepository>();
            services.AddTransient<IContactDetailTypeRepository, ContactDetailTypeRepository>();
            services.AddTransient<IAddressRepository, AddressRepository>();
            services.AddTransient<ICurrencyRepository, CurrencyRepository>();
            services.AddTransient<ICurrencyRateRepository, CurrencyRateRepository>();

            // http client
            services.AddHttpClient("NBPClient", options =>
            {
                options.Timeout = new TimeSpan(0, 0, 15);
                options.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            }).ConfigurePrimaryHttpMessageHandler(sp => new HttpClientHandler());
            services.AddScoped<INBPClient, NBPClient>();

            return services;
        }
    }
}
