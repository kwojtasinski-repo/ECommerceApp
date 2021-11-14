using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Item;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.OrderItem
{
    public class NewOrderItemVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.OrderItem>
    {
        public int ItemId { get; set; }   // 1:Many Item OrderItem  
        public int ItemOrderQuantity { get; set; }
        public int? OrderId { get; set; }  // Many : 1 OrderItem Order
        public string UserId { get; set; }
        public int? CouponUsedId { get; set; }
        public int? RefundId { get; set; } // 1:Many Refund OrderItem
        public string ItemName { get; set; }
        public decimal ItemCost { get; set; }

        public List<ItemsAddToCartVm> Items { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewOrderItemVm, ECommerceApp.Domain.Model.OrderItem>().ReverseMap()
                .ForMember(i => i.ItemName, opt => opt.MapFrom(o => o.Item.Name))
                .ForMember(i => i.ItemCost, opt => opt.MapFrom(o => o.Item.Cost))
                .ForMember(i => i.Items, opt => opt.Ignore());
        }
    }

    public class NewOrderItemValidation : AbstractValidator<NewOrderItemVm>
    {
        public NewOrderItemValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.ItemId).NotNull();
            RuleFor(x => x.ItemOrderQuantity).NotNull();
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}
