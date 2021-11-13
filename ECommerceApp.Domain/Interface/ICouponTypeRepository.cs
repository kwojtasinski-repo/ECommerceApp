﻿using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ICouponTypeRepository : IGenericRepository<CouponType>
    {
        void DeleteCouponType(int couponTypeId);
        int AddCouponType(CouponType couponType);
        CouponType GetCouponTypeById(int couponTypeId);
        IQueryable<CouponType> GetAllCouponTypes();
        void UpdateCouponType(CouponType couponType);
    }
}