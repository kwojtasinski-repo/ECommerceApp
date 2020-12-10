using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Customer;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class PaymentForListVm : IMapFrom<ECommerceApp.Domain.Model.Payment>
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }  // 1:Many Customer Payment
        public CustomerForListVm Customer { get; set; }
        public int OrderId { get; set; } // 1:1 Payment Order
        public virtual OrderForListVm Order { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Payment, PaymentForListVm>();
        }
    }
}
