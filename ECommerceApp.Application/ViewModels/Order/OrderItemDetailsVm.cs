using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Item;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderItemDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.OrderItem>
    {
        public int ItemId { get; set; }   // 1:Many Item OrderItem  
        public int ItemOrderQuantity { get; set; }
        public virtual ItemDetailsVm Item { get; set; }
        public string UserId { get; set; }
        public int? OrderId { get; set; }  // Many : 1 OrderItem Order
        public OrderDetailsVm Order { get; set; }
        public int? CouponUsedId { get; set; }
        public CouponUsedDetailsVm CouponUsed { get; set; } // 1:Many OrderItem Coupon discount can be used for many Items
        public int? RefundId { get; set; } // 1:Many Refund OrderItem
        public RefundDetailsVm Refund { get; set; }
        public string ItemName { get; set; }
        public decimal ItemCost { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<OrderItemDetailsVm, ECommerceApp.Domain.Model.OrderItem>().ReverseMap()
                .ForMember(i => i.ItemName, opt => opt.MapFrom(o => o.Item.Name))
                .ForMember(i => i.ItemCost, opt => opt.MapFrom(o => o.Item.Cost));
        }
    }
}
