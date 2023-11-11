using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services.Coupons
{
    // TODO: Strategy with coupons assiging using
    // one could be used for some time, another no limit, another one order, another sepcial to user and etc
    // make it possible maybe for not all purposes aboves but more dynamic :)
    public class CouponService : AbstractService<CouponVm, ICouponRepository, Coupon>, ICouponService
    {
        public CouponService(ICouponRepository couponRepo, IMapper mapper) : base(couponRepo, mapper)
        { }

        public int AddCoupon(CouponVm couponVm)
        {
            if (couponVm is null)
            {
                throw new BusinessException($"{typeof(CouponVm).Name} cannot be null");
            }

            if (couponVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            if (couponVm.Discount < 1 || couponVm.Discount > 99)
            {
                throw new BusinessException("Discount should be inclusive between 1 and 99");
            }

            var id = Add(couponVm);
            return id;
        }

        public void DeleteCoupon(int id)
        {
            Delete(id);
        }

        public List<CouponVm> GetAll(string searchString)
        {
            var coupons = _repo.GetAllCoupons().Where(coupon => coupon.Code.StartsWith(searchString))
                .ProjectTo<CouponVm>(_mapper.ConfigurationProvider)
                .ToList();
            return coupons;
        }

        public ListForCouponVm GetAllCoupons(int pageSize, int pageNo, string searchString)
        {
            var coupons = _repo.GetAllCoupons().Where(coupon => coupon.Code.StartsWith(searchString))
                .Skip(pageSize * (pageNo - 1)).Take(pageSize)
                .ProjectTo<CouponVm>(_mapper.ConfigurationProvider);
            var couponsToShow = coupons.ToList();

            var couponsList = new ListForCouponVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Coupons = couponsToShow,
                Count = couponsToShow.Count
            };

            return couponsList;
        }

        public CouponDetailsVm GetCouponDetail(int id)
        {
            var couponDetails = _repo.GetCouponById(id);
            var couponDetailsVm = _mapper.Map<CouponDetailsVm>(couponDetails);
            return couponDetailsVm;
        }

        public CouponVm GetCoupon(int id)
        {
            var couponVm = Get(id);
            return couponVm;
        }

        public CouponVm GetCouponFirstOrDefault(Expression<Func<Coupon, bool>> expression)
        {
            var coupon = _repo.GetAll().Where(expression).AsNoTracking().FirstOrDefault();
            var couponVm = _mapper.Map<CouponVm>(coupon);
            return couponVm;
        }

        public void UpdateCoupon(CouponVm couponVm)
        {
            if (couponVm is null)
            {
                throw new BusinessException($"{typeof(CouponVm).Name} cannot be null");
            }

            if (couponVm.Discount < 1 || couponVm.Discount > 99)
            {
                throw new BusinessException("Discount should be inclusive between 1 and 99");
            }

            Update(couponVm);
        }

        public IEnumerable<CouponVm> GetAllCoupons(Expression<Func<Coupon, bool>> expression)
        {
            var coupons = _repo.GetAllCoupons().Where(expression).AsNoTracking()
                .ProjectTo<CouponVm>(_mapper.ConfigurationProvider);
            var couponsToShow = coupons.ToList();

            return couponsToShow;
        }

        public void DeleteCouponUsed(int couponId, int couponUsedId)
        {
            var coupon = _repo.GetAll().Where(c => c.Id == couponId && c.CouponUsedId == couponUsedId).FirstOrDefault();

            if (coupon is null)
            {
                throw new BusinessException("Given invalid id");
            }

            _repo.DetachEntity(coupon);
            coupon.CouponUsedId = null;
            _repo.Update(coupon);
        }

        public void AddCouponUsed(int couponId, int couponUsedId)
        {
            var coupon = _repo.GetAll().Where(c => c.Id == couponId).FirstOrDefault();

            if (coupon is null)
            {
                throw new BusinessException("Given invalid id");
            }

            _repo.DetachEntity(coupon);
            coupon.CouponUsedId = couponUsedId;
            _repo.Update(coupon);
        }

        public CouponVm GetCouponByCode(string promoCode)
        {
            var coupon = _repo.GetAll().Where(c => c.Code == promoCode).FirstOrDefault();
            var couponVm = _mapper.Map<CouponVm>(coupon);
            return couponVm;
        }

        public int CheckPromoCode(string code)
        {
            var coupons = GetAllCoupons(c => true);
            var coupon = coupons.FirstOrDefault(c => string.Equals(c.Code, code,
                   StringComparison.Ordinal) && c.CouponUsedId == null);
            var id = 0;
            if (coupon != null)
            {
                id = coupon.Id;
            }
            return id;
        }
    }
}
