﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerPaymentsVm : BaseVm, IMapFrom<Payment>
    {
        public int Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }  // 1:Many Customer Payment
        public int OrderId { get; set; } // 1:1 Payment Order

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Payment, CustomerPaymentsVm>()
                .ForMember(p => p.Id, opt => opt.MapFrom(pay => pay.Id))
                .ForMember(p => p.Number, opt => opt.MapFrom(pay => pay.Number))
                .ForMember(p => p.DateOfOrderPayment, opt => opt.MapFrom(pay => pay.DateOfOrderPayment))
                .ForMember(p => p.CustomerId, opt => opt.MapFrom(pay => pay.CustomerId))
                .ForMember(p => p.OrderId, opt => opt.MapFrom(pay => pay.OrderId));
        }
    }
}