using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Order;
using FluentValidation;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class NewCouponUsedVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.CouponUsed>
    {
        public int CouponId { get; set; }
        public string Code { get; set; }
        public int OrderId { get; set; } // OrderId for order discount relation 1:1
        public int Number { get; set; }


        public void Mapping(Profile profile)
        {
            profile.CreateMap<NewCouponUsedVm, ECommerceApp.Domain.Model.CouponUsed>().ReverseMap()
                .ForMember(c => c.Code, opt => opt.MapFrom(c => c.Coupon.Code))
                //  .ForPath(c => c.Coupon.Code, opt => opt.MapFrom(co => co.Code))
                .ForMember(n => n.Number, opt => opt.MapFrom(n => n.Order.Number));
              //  .ForPath(o => o.Order.Number, opt => opt.MapFrom(n => n.Number));
        }
    }

    public class NewCouponUsedValidation : AbstractValidator<NewCouponUsedVm>
    {
        public NewCouponUsedValidation()
        {
            RuleFor(x => x.Id).NotNull();
            RuleFor(x => x.Code).NotNull();
            RuleFor(x => x.CouponId).InclusiveBetween(0, 99);
            RuleFor(x => x.Number).NotNull();
            RuleFor(x => x.OrderId).NotNull();
        }
    }
}