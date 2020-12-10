using AutoMapper;
using ECommerceApp.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.ViewModels.Coupon
{
    public class NewCouponTypeVm : IMapFrom<ECommerceApp.Domain.Model.CouponType>
    {
        public int Id { get; set; }
        public string Type { get; set; } // Type Coupon for only one Order; for only one Item

        public void Mapping(Profile profile)
        {
            profile.CreateMap<ECommerceApp.Domain.Model.CouponType, NewCouponTypeVm>().ReverseMap();
        }
    }
}
