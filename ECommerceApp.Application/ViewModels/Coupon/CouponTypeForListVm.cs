using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class CouponTypeForListVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.CouponType>
    {
        public string Type { get; set; } // Type Coupon for only one Order; for only one Item

        public ICollection<CouponForListVm> Coupons { get; set; } // 1:Many CouponType Coupon

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.CouponType, CouponTypeForListVm>();
        }
    }

    public class CouponTypeForListValidation : AbstractValidator<CouponTypeForListVm>
    {
        public CouponTypeForListValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Type).NotNull();
        }
    }
}
