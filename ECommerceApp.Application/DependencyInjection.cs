﻿using AutoMapper;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Application.ViewModels.Order;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ECommerceApp.Application
{
    public static class DependencyInjection 
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddTransient<IItemService, ItemService>();
            services.AddTransient<ICustomerService, CustomerService>();
            services.AddTransient<IOrderService, OrderService>();
            services.AddTransient<ICouponService, CouponService>();
            services.AddTransient<IUserService, UserService>();
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            services.AddTransient<IFileStore, FileStore>();
            services.AddTransient<IFileWrapper, FileWrapper>();
            services.AddTransient<IDirectoryWrapper, DirectoryWrapper>();
            services.AddTransient<IImageService, ImageService>();
            services.AddTransient<ICouponTypeService, CouponTypeService>();
            services.AddTransient<ICouponUsedService, CouponUsedService>();
            services.AddTransient<IBrandService, BrandService>();
            services.AddTransient<ITypeService, TypeService>();
            services.AddTransient<ITagService, TagService>();
            services.AddTransient<IRefundService, RefundService>();
            services.AddTransient<IPaymentService, PaymentService>();
            services.AddTransient<IOrderItemService, OrderItemService>();
            services.AddTransient<IContactDetailService, ContactDetailService>();
            services.AddTransient<IContactDetailTypeService, ContactDetailTypeService>();
            services.AddTransient<IAddressService, AddressService>();
            return services;
        }
    }
}
