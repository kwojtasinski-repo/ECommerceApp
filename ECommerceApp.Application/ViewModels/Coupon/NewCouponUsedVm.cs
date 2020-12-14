using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Order;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class NewCouponUsedVm : IMapFrom<ECommerceApp.Domain.Model.CouponUsed>
    {
        public int Id { get; set; }
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
}