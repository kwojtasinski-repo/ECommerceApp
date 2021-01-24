using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Order;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class CouponUsedForListVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.CouponUsed>
    {
        public int CouponId { get; set; }
        public int OrderId { get; set; } // OrderId for order discount relation 1:1
        public string Code { get; set; }
        public int Number { get; set; }

        public ICollection<OrderForListVm> OrderItems { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.CouponUsed, CouponUsedForListVm>()
                .ForMember(c => c.Code, opt => opt.MapFrom(co => co.Coupon.Code))
                .ForMember(n => n.Number, opt => opt.MapFrom(o => o.Order.Number))
                .ForMember(oi => oi.OrderItems, opt => opt.MapFrom(orit => orit.OrderItems));
        }
    }

    public class CouponUsedForListValidation : AbstractValidator<CouponUsedForListVm>
    {
        public CouponUsedForListValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.CouponId).NotNull();
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}
