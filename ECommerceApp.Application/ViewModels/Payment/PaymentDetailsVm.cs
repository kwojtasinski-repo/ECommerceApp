using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Order;
using System;

namespace ECommerceApp.Application.ViewModels.Payment
{
    public class PaymentDetailsVm
    {
        public PaymentDetailsDto Payment { get; set; }
    }
}