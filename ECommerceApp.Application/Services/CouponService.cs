using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class CouponService : AbstractService<CouponVm, ICouponRepository, Coupon>, ICouponService
    {
        public CouponService(ICouponRepository couponRepo, IMapper mapper) : base(couponRepo, mapper)
        { }

        public int AddCoupon(CouponVm couponVm)
        {
            if (couponVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
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

        public void UpdateCoupon(CouponVm couponVm)
        {
            if (couponVm != null)
            {
                Update(couponVm);
            }
        }

        public IEnumerable<CouponVm> GetAllCoupons(Expression<Func<Coupon,bool>> expression)
        {
            var coupons = _repo.GetAllCoupons().Where(expression)
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
    }
}
