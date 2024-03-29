﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.CouponType;
using ECommerceApp.Application.ViewModels.CouponUsed;
using FluentValidation;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class CouponDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Coupon>
    {
        public string Code { get; set; }
        public int Discount { get; set; }
        public string Description { get; set; }
        public int CouponTypeId { get; set; } // 1:Many CouponType Coupon
        public CouponTypeDetailsVm Type { get; set; }
        public int? CouponUsedId { get; set; } // 1:1 Coupon CouponUsed can be null
        public int Number { get; set; }
        public CouponUsedDetailsVm CouponUsed { get; set; } // test with ForPath because ForMember will not work

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Coupon, CouponDetailsVm>()
                 .ForMember(c => c.Type, opts => opts.MapFrom(t => t.Type))
                 //  .ForPath(c => c.Type.Type, opts => opts.MapFrom(t => t.Type))
                 .ForMember(c => c.Number, opt => opt.MapFrom(n => n.CouponUsed.Order.Number))
               // .ForPath(c => c.CouponUsed.Order.Number, opt => opt.MapFrom(n => n.Number));
               .ReverseMap();
        }

        public class CouponDetailsValidation : AbstractValidator<CouponDetailsVm>
        {
            public CouponDetailsValidation()
            {
                RuleFor(x => x.Id).NotNull();
                RuleFor(x => x.Discount).InclusiveBetween(0,99);
                RuleFor(x => x.Description).MaximumLength(255);
            }
        }
    }
}
