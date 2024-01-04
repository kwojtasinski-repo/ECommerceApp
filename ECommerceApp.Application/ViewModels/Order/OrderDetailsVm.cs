using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.OrderItem;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class OrderDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Order>
    {
        public string Number { get; set; }
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
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; }

        public List<OrderItemDetailsVm> OrderItems { get; set; } // 1:Many relation

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Order, OrderDetailsVm>()
                .ForMember(o => o.CurrencyCode, src => src.MapFrom(o => o.Currency.Code))
                .ReverseMap()
                .ForMember(o => o.Currency, src => src.Ignore());
        }
    }
}
