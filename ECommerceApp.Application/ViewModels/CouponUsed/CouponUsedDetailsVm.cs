using AutoMapper;
using ECommerceApp.Application.Mapping;

namespace ECommerceApp.Application.ViewModels.CouponUsed
{
    public class CouponUsedDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.CouponUsed>
    {
        public int CouponId { get; set; }
        public string Code { get; set; }
        public int OrderId { get; set; }
        public int Number { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.CouponUsed, CouponUsedDetailsVm>()
                //  .ForMember(c => c.Coupon.Code, opt => opt.MapFrom(co => co.Code))
                .ForPath(c => c.Code, opt => opt.MapFrom(co => co.Coupon.Code))
                //   .ForMember(c => c.Order.Number, opt => opt.MapFrom(n => n.Number));
                .ForPath(c => c.Number, opt => opt.MapFrom(n => n.Order.Number));
        }
    }
}
