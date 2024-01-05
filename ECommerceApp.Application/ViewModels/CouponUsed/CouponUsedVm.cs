using AutoMapper;
using ECommerceApp.Application.Mapping;
using FluentValidation;

namespace ECommerceApp.Application.ViewModels.CouponUsed
{
    public class CouponUsedVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.CouponUsed>
    {
        public int CouponId { get; set; }
        public int OrderId { get; set; } // OrderId for order discount relation 1:1


        public void Mapping(Profile profile)
        {
            profile.CreateMap<CouponUsedVm, ECommerceApp.Domain.Model.CouponUsed>().ReverseMap();
                //  .ForPath(c => c.Coupon.Code, opt => opt.MapFrom(co => co.Code))
              //  .ForPath(o => o.Order.Number, opt => opt.MapFrom(n => n.Number));
        }
    }

    public class CouponUsedVmValidation : AbstractValidator<CouponUsedVm>
    {
        public CouponUsedVmValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.CouponId).NotNull();
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}