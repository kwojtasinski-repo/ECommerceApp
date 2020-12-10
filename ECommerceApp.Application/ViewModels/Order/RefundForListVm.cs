using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Customer;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class RefundForListVm : IMapFrom<ECommerceApp.Domain.Model.Refund>
    {
        public int Id { get; set; }
        public string Reason { get; set; }
        public bool Accepted { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public int CustomerId { get; set; }
        public CustomerForListVm Customer { get; set; } // 1:Many one customer can refund many orders
        public int OrderId { get; set; } // 1:1 Only one Order can be refund
        public OrderForListVm Order { get; set; }

        public ICollection<OrderItemForListVm> OrderItems { get; set; } // 1:Many with OrderItems

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Refund, RefundForListVm>();
        }
    }
}
