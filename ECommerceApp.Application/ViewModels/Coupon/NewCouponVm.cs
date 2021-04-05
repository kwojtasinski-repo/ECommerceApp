﻿using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class NewCouponVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.Coupon>
    {
        public string Code { get; set; }
        public int Discount { get; set; }
        public string Description { get; set; }
        public int CouponTypeId { get; set; } // 1:Many CouponType Coupon
        public int? CouponUsedId { get; set; } // 1:1 Coupon CouponUsed can be null

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Coupon, NewCouponVm>().ReverseMap();
        }
    }

    public class NewCouponValidation : AbstractValidator<NewCouponVm>
    {
        public NewCouponValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Code).NotNull();
            RuleFor(x => x.Discount).InclusiveBetween(0,99);
            RuleFor(x => x.Description).MaximumLength(255);
            RuleFor(x => x.CouponTypeId).NotNull();
        }
    }
}
