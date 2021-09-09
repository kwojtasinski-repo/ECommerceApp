using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class GetOrderItemVm : BaseVm, IMapFrom<NewOrderItemVm>
    {
        public int ItemId { get; set; }   // 1:Many Item OrderItem  
        public int ItemOrderQuantity { get; set; }
        public int? OrderId { get; set; }  // Many : 1 OrderItem Order
        public string UserId { get; set; }
        public int? CouponUsedId { get; set; }
        public int? RefundId { get; set; } // 1:Many Refund OrderItem
        public string ItemName { get; set; }
        public decimal ItemCost { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewOrderItemVm, GetOrderItemVm>()
                .ForMember(oi => oi.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(oi => oi.ItemId, opt => opt.MapFrom(src => src.ItemId))
                .ForMember(oi => oi.ItemOrderQuantity, opt => opt.MapFrom(src => src.ItemOrderQuantity))
                .ForMember(oi => oi.OrderId, opt => opt.MapFrom(src => src.OrderId))
                .ForMember(oi => oi.UserId, opt => opt.MapFrom(src => src.UserId))
                .ForMember(oi => oi.CouponUsedId, opt => opt.MapFrom(src => src.CouponUsedId))
                .ForMember(oi => oi.RefundId, opt => opt.MapFrom(src => src.RefundId))
                .ForMember(oi => oi.ItemName, opt => opt.MapFrom(src => src.ItemName))
                .ForMember(oi => oi.ItemCost, opt => opt.MapFrom(src => src.ItemCost));
        }
    }
}
