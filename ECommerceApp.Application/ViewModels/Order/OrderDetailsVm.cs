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
        public int? PaymentId { get; set; }
        public bool IsPaid { get; set; }
        public int? RefundId { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; }
        public string Discount { get; set; }
        public string PaymentNumber { get; set; }
        public int PaymentCurrencyId { get; set; }
        public string PaymentCurrencyCode { get; set; }
        public bool AcceptedRefund { get; set; }
        public string ReasonRefund { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public string CustomerInformation { get; set; }

        public List<OrderItemDetailsVm> OrderItems { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Order, OrderDetailsVm>()
                .ForMember(o => o.CurrencyCode, src => src.MapFrom(o => o.Currency.Code))
                .ForMember(o => o.PaymentCurrencyCode, src => src.MapFrom(o => o.Payment.Currency.Code))
                .ForMember(o => o.PaymentNumber, src => src.MapFrom(o => o.Payment.Number))
                .ForMember(o => o.PaymentCurrencyId, src => src.MapFrom(o => o.Payment.CurrencyId))
                .ForMember(r => r.Discount, opt => opt.MapFrom(r => r.CouponUsed.Coupon.Discount))
                .ForMember(rf => rf.ReasonRefund, opt => opt.MapFrom(r => r.Refund.Reason))
                .ForMember(af => af.AcceptedRefund, opt => opt.MapFrom(a => a.Refund.Accepted))
                .ForMember(rd => rd.RefundDate, opt => opt.MapFrom(rd => rd.Refund.RefundDate))
                .ForMember(ow => ow.OnWarranty, opt => opt.MapFrom(ow => ow.Refund.OnWarranty))
                .ForMember(i => i.CustomerInformation, opt => opt.MapFrom(c => (c.Customer.NIP != null && c.Customer.CompanyName != null)
                            ? c.Customer.FirstName + " " + c.Customer.LastName + " " + c.Customer.NIP + " " + c.Customer.CompanyName
                            : c.Customer.FirstName + " " + c.Customer.LastName))
                .ReverseMap()
                .ForMember(o => o.Currency, src => src.Ignore())
                .ForMember(o => o.Payment, src => src.Ignore())
                .ForMember(o => o.Customer, src => src.Ignore())
                .ForMember(o => o.CouponUsed, src => src.Ignore())
                .ForMember(o => o.Refund, src => src.Ignore());
        }
    }
}
