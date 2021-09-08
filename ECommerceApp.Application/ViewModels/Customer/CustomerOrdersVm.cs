using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Customer
{
    public class CustomerOrdersVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Order>
    {
        public int Number { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public int? CouponUsedId { get; set; }
        public int CustomerId { get; set; }
        public string UserId { get; set; }
        public int? PaymentId { get; set; } // 1:1 Order Payment
        public bool IsPaid { get; set; }
        public int? RefundId { get; set; } // 1:1 Order Refund

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Order, CustomerOrdersVm>()
                .ForMember(o => o.Id, opt => opt.MapFrom(or => or.Id))
                .ForMember(o => o.Number, opt => opt.MapFrom(or => or.Number))
                .ForMember(o => o.Cost, opt => opt.MapFrom(or => or.Cost))
                .ForMember(o => o.Ordered, opt => opt.MapFrom(or => or.Ordered))
                .ForMember(o => o.Delivered, opt => opt.MapFrom(or => or.Delivered))
                .ForMember(o => o.IsDelivered, opt => opt.MapFrom(or => or.IsDelivered))
                .ForMember(o => o.CouponUsedId, opt => opt.MapFrom(or => or.CouponUsedId))
                .ForMember(o => o.CustomerId, opt => opt.MapFrom(or => or.CustomerId))
                .ForMember(o => o.UserId, opt => opt.MapFrom(or => or.UserId))
                .ForMember(o => o.PaymentId, opt => opt.MapFrom(or => or.PaymentId))
                .ForMember(o => o.IsPaid, opt => opt.MapFrom(or => or.IsPaid))
                .ForMember(o => o.RefundId, opt => opt.MapFrom(or => or.RefundId));
        }
    }
}
