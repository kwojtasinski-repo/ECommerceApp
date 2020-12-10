using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Customer;
using System;

namespace ECommerceApp.Application.ViewModels.Order
{ 
    public class NewPaymentVm : IMapFrom<ECommerceApp.Domain.Model.Payment>
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public DateTime DateOfOrderPayment { get; set; }
        public int CustomerId { get; set; }  // 1:Many Customer Payment
        public NewCustomerVm Customer { get; set; }
        public int OrderId { get; set; } // 1:1 Payment Order
        public virtual NewOrderVm Order { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewPaymentVm, ECommerceApp.Domain.Model.Payment>().ReverseMap();
        }
}
}