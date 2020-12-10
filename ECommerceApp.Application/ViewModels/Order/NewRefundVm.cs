using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Customer;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class NewRefundVm : IMapFrom<ECommerceApp.Domain.Model.Refund>
    {
        public int Id { get; set; }
        public string Reason { get; set; }
        public bool Accepted { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public int CustomerId { get; set; }
        public NewCustomerVm Customer { get; set; } // 1:Many one customer can refund many orders
        public int OrderId { get; set; } // 1:1 Only one Order can be refund
        public NewOrderVm Order { get; set; }

        public ICollection<NewOrderItemVm> OrderItems { get; set; } // 1:Many with OrderItems

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewRefundVm, ECommerceApp.Domain.Model.Refund>().ReverseMap();
        }
}
}