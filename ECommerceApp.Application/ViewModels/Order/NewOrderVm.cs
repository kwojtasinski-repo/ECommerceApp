using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Application.ViewModels.Item;
using FluentValidation;
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
        public string UserId { get; set; }
        public int? PaymentId { get; set; } // 1:1 Order Payment
        public bool IsPaid { get; set; }
        public int? RefundId { get; set; } // 1:1 Order Refund
        public string RefCode { get; set; }
        public int CouponId { get; set; }
        public double CostToConvert { get; set; }
        public string ReasonRefund { get; set; }
        public bool AcceptedRefund { get; set; }
        public DateTime RefundDate { get; set; }
        public bool OnWarranty { get; set; }
        public bool ChangedCode { get; set; }
        public bool ChangedRefund { get; set; }


        public List<NewOrderItemVm> OrderItems { get; set; } // 1:Many relation
        public List<ECommerceApp.Domain.Model.Item> Items { get; set; }
        public NewCustomerVm NewCustomer { get; set; }
        public bool CustomerData { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewOrderVm, ECommerceApp.Domain.Model.Order>().ReverseMap()
                .ForMember(r => r.RefCode, opt => opt.MapFrom(r => r.CouponUsed.Coupon))
                .ForMember(c => c.CouponId, opt => opt.Ignore())
                .ForMember(i => i.Items, opt => opt.Ignore())
                .ForMember(c => c.CostToConvert, opt => opt.Ignore())
                .ForMember(rf => rf.ReasonRefund, opt => opt.MapFrom(r => r.Refund.Reason))
                .ForMember(af => af.AcceptedRefund, opt => opt.MapFrom(a => a.Refund.Accepted))
                .ForMember(rd => rd.RefundDate, opt => opt.MapFrom(rd => rd.Refund.RefundDate))
                .ForMember(ow => ow.OnWarranty, opt => opt.MapFrom(ow => ow.Refund.OnWarranty))
                .ForMember(cc => cc.ChangedCode, opt => opt.Ignore())
                .ForMember(cr => cr.ChangedRefund, opt => opt.Ignore())
                .ForMember(c => c.NewCustomer, opt => opt.Ignore())
                .ForMember(c => c.CustomerData, opt => opt.Ignore());
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
