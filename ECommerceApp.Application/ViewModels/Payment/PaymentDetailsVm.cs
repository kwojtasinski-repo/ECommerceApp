using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Order;
using System;

namespace ECommerceApp.Application.ViewModels.Payment
{
    public class PaymentDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Payment>
    {
        public int Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }  // 1:Many Customer Payment
        public CustomerDetailsVm Customer { get; set; }
        public int OrderId { get; set; } // 1:1 Payment Order
        public virtual OrderDetailsVm Order { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Payment, PaymentDetailsVm>().ReverseMap();
        }
    }
}