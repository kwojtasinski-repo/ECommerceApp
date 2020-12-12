using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class NewOrderVm : IMapFrom<ECommerceApp.Domain.Model.Order>
    {
        public NewOrderVm()
        {
            OrderItems = new List<NewOrderItemVm>();
            Items = new List<Domain.Model.Item>();
        }

        public int Id { get; set; }
        public int Number { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public int? CouponUsedId { get; set; }
        public int CustomerId { get; set; }
        public int? PaymentId { get; set; } // 1:1 Order Payment
        public bool IsPaid { get; set; }
        public int? RefundId { get; set; } // 1:1 Order Refund
        public string RefCode { get; set; }
        public int CouponId { get; set; }
        public double CostToConvert { get; set; }

        public List<NewOrderItemVm> OrderItems { get; set; } // 1:Many relation
        public List<ECommerceApp.Domain.Model.Item> Items { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewOrderVm, ECommerceApp.Domain.Model.Order>().ReverseMap()
                .ForMember(r => r.RefCode, opt => opt.Ignore())
                .ForMember(c => c.CouponId, opt => opt.Ignore())
                .ForMember(i => i.Items, opt => opt.Ignore())
                .ForMember(c => c.CostToConvert, opt => opt.Ignore());
        }
}
}
