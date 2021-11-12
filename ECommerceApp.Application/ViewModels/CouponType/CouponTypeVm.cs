using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class CouponTypeVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.CouponType>
    {
        public string Type { get; set; } // Type Coupon for only one Order; for only one Item

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.CouponType, CouponTypeVm>()
                .ForMember(ct => ct.Id, map => map.MapFrom(src => src.Id))
                .ForMember(ct => ct.Type, map => map.MapFrom(src => src.Type))
                .ReverseMap();
        }
    }

    public class NewCouponTypeValidation : AbstractValidator<CouponTypeVm>
    {
        public NewCouponTypeValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Type).NotNull();
        }
    }
}
