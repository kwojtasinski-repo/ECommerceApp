using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Item;
using FluentValidation;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.Order
{
    public class NewOrderVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Order>
    {
        public NewOrderVm()
        {
            OrderItems = new List<OrderItemDto>();
            Items = new List<ItemVm>();
        }

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
        public string PromoCode { get; set; }
        public int CouponId { get; set; }
        public double CostToConvert { get; set; }
        public string ReasonRefund { get; set; }
        public bool AcceptedRefund { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public bool ChangedCode { get; set; }
        public bool ChangedRefund { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; }


        public List<OrderItemDto> OrderItems { get; set; } // 1:Many relation
        public List<ItemVm> Items { get; set; }
        public CustomerDetailsDto NewCustomer { get; set; }
        public bool CustomerData { get; set; }
        public string CustomerInformation { get; set; }
        public string PaymentNumber { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewOrderVm, ECommerceApp.Domain.Model.Order>().ReverseMap()
                .ForMember(r => r.PromoCode, opt => opt.MapFrom(r => r.CouponUsed != null ? r.CouponUsed.Coupon.Discount : 0))
                .ForMember(c => c.CouponId, opt => opt.MapFrom(r => r.CouponUsed != null ? r.CouponUsed.Coupon.Id : 0))
                .ForMember(i => i.Items, opt => opt.Ignore())
                .ForMember(c => c.CostToConvert, opt => opt.Ignore())
                .ForMember(rf => rf.ReasonRefund, opt => opt.MapFrom(r => r.Refund != null ? r.Refund.Reason : ""))
                .ForMember(af => af.AcceptedRefund, opt => opt.MapFrom(a => a.Refund != null && a.Refund.Accepted))
                .ForMember(rd => rd.RefundDate, opt => opt.MapFrom(rd => rd.Refund != null ? rd.Refund.RefundDate : new DateTime()))
                .ForMember(ow => ow.OnWarranty, opt => opt.MapFrom(ow => ow.Refund != null && ow.Refund.OnWarranty))
                .ForMember(cc => cc.ChangedCode, opt => opt.Ignore())
                .ForMember(cr => cr.ChangedRefund, opt => opt.Ignore())
                .ForMember(c => c.NewCustomer, opt => opt.Ignore())
                .ForMember(c => c.CustomerData, opt => opt.Ignore())
                .ForMember(c => c.CurrencyName, opt => opt.MapFrom(c => c.Currency.Code))
                .ForMember(p => p.PaymentNumber, opt => opt.MapFrom(p => p.Payment != null ? p.Payment.Number : ""))
                .ForMember(i => i.CustomerInformation, opt => opt.MapFrom(c => 
                    c.Customer != null
                    ? ((c.Customer.NIP != null && c.Customer.CompanyName != null)
                         ? c.Customer.FirstName + " " + c.Customer.LastName + " " + c.Customer.NIP + " " + c.Customer.CompanyName
                         : c.Customer.FirstName + " " + c.Customer.LastName)
                    : "")
                );
        }
    }

    public class NewOrderValidation : AbstractValidator<NewOrderVm>
    {
        public NewOrderValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Number).NotNull();
            RuleFor(x => x.Cost).NotNull();
            RuleFor(x => x.Ordered).NotNull();
            RuleFor(x => x.CustomerId).NotNull();
        }
    }
}
