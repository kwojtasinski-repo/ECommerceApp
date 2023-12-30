using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Order;
using System;

namespace ECommerceApp.Application.ViewModels.Refund
{
    public class RefundDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Refund>
    {
        public string Reason { get; set; }
        public bool Accepted { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public int CustomerId { get; set; }
        public CustomerDto Customer { get; set; } // 1:Many one customer can refund many orders
        public int OrderId { get; set; } // 1:1 Only one Order can be refund
        public OrderVm Order { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Refund, RefundDetailsVm>().ReverseMap();
        }
    }
}