using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class CouponDetailsVm : IMapFrom<ECommerceApp.Domain.Model.Coupon>
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public int Discount { get; set; }
        public string Description { get; set; }
        public int CouponTypeId { get; set; } // 1:Many CouponType Coupon
        public CouponTypeDetailsVm Type { get; set; }
        public int? CouponUsedId { get; set; } // 1:1 Coupon CouponUsed can be null
        public int Number { get; set; }
        //public CouponUsedDetailsVm CouponUsed { get; set; } // test with ForPath because ForMember will not work

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Coupon, CouponDetailsVm>().ReverseMap()
                 .ForMember(c => c.Type, opts => opts.MapFrom(t => t.Type))
               // .ForPath(c => c.Type.Type, opts => opts.MapFrom(t => t.TypeName))
               // .ForMember(c => c.CouponUsed.Order.Number, opt => opt.MapFrom(n => n.Number));
                .ForPath(c => c.CouponUsed.Order.Number, opt => opt.MapFrom(n => n.Number));
        }
    }
}
