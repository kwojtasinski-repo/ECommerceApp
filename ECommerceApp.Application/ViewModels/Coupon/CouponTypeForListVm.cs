using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class CouponTypeForListVm : IMapFrom<ECommerceApp.Domain.Model.CouponType>
    {
        public int Id { get; set; }
        public string Type { get; set; } // Type Coupon for only one Order; for only one Item

        public ICollection<CouponForListVm> Coupons { get; set; } // 1:Many CouponType Coupon

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.CouponType, CouponTypeForListVm>();
        }
    }
}
