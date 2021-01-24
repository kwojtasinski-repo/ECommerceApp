using AutoMapper;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Order;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class CouponUsedDetailsVm : BaseVm, IMapFrom<ECommerceApp.Domain.Model.CouponUsed>
    {
        public int CouponId { get; set; }
        public string Code { get; set; }
        public int OrderId { get; set; } // OrderId for order discount relation 1:1
        public int Number { get; set; }

        public ICollection<OrderItemDetailsVm> OrderItems { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.CouponUsed, CouponUsedDetailsVm>().ReverseMap()
              //  .ForMember(c => c.Coupon.Code, opt => opt.MapFrom(co => co.Code))
                .ForPath(c => c.Coupon.Code, opt => opt.MapFrom(co => co.Code))
             //   .ForMember(c => c.Order.Number, opt => opt.MapFrom(n => n.Number));
                .ForPath(c => c.Order.Number, opt => opt.MapFrom(n => n.Number));
        }
    }
}
