using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class CouponForListVm : IMapFrom<ECommerceApp.Domain.Model.Coupon>
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public int Discount { get; set; }
        public string Description { get; set; }
        public int CouponTypeId { get; set; } // 1:Many CouponType Coupon
        public int? CouponUsedId { get; set; } // 1:1 Coupon CouponUsed can be null

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.Coupon, CouponForListVm>();
        }
    }
}
