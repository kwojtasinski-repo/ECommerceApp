using AutoMapper;
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
            /*services.AddTransient<IBaseService<NewItemVm>, ItemServiceAbstract>();
            services.AddTransient<IBaseService<NewCustomerVm>, CustomerServiceAbstract>();
            services.AddTransient<IBaseService<NewOrderVm>, OrderServiceAbstract>();
            services.AddTransient<IBaseService<NewCouponVm>, CouponServiceAbstract>();*/
            services.AddTransient<ItemServiceAbstract, ItemService>();
            services.AddTransient<CustomerServiceAbstract, CustomerService>();
            services.AddTransient<OrderServiceAbstract, OrderService>();
            services.AddTransient<CouponServiceAbstract, CouponService>();
            services.AddTransient<IUserService, UserService>();
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            return services;
        }
    }
}
