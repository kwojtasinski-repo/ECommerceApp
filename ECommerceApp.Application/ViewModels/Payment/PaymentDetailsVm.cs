﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Order;
using System;

namespace ECommerceApp.Application.ViewModels.Payment
{
    public class PaymentDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Payment>
    {
        public string Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }  // 1:Many Customer Payment
        public CustomerDetailsVm Customer { get; set; }
        public int OrderId { get; set; } // 1:1 Payment Order
        public virtual OrderDetailsVm Order { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Payment, PaymentDetailsVm>()
                .ForMember(p => p.CurrencyName, opt => opt.MapFrom(src => src.Currency.Code))
                .ReverseMap();
        }
    }
}