using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Coupon;
using FluentValidation;
using System.Collections.Generic;

namespace ECommerceApp.Application.ViewModels.CouponType
{
    public class CouponTypeDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.CouponType>
    {
        public string Type { get; set; }

        public ICollection<CouponDetailsVm> Coupons { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.CouponType, CouponTypeDetailsVm>().ReverseMap();
        }
    }

    public class CouponTypeDetailsValidation : AbstractValidator<CouponTypeDetailsVm>
    {
        public CouponTypeDetailsValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Type).NotNull();
        }
    }
}
