using AutoMapper;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

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
                throw new BusinessException("Discount should be inclusive between 1 and 99", "couponInvalidDiscount");
            }

            if (_repo.ExistsByCode(couponVm.Code))
            {
                throw new BusinessException($"Coupon with code '{couponVm.Code}' already exists", "couponCodeAlreadyExists", new Dictionary<string, string> { { "code", couponVm.Code } });
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
            return _mapper.Map<List<CouponVm>>(_repo.GetAllCoupons(searchString));
        }

        public ListForCouponVm GetAllCoupons(int pageSize, int pageNo, string searchString)
        {
            var coupons = _mapper.Map<List<CouponVm>>(_repo.GetAllCoupons(pageSize, pageNo, searchString));

            var couponsList = new ListForCouponVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Coupons = coupons,
                Count = _repo.GetCountBySearchString(searchString)
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
            if (couponVm is null)
            {
                throw new BusinessException($"{typeof(CouponVm).Name} cannot be null");
            }

            if (couponVm.Discount < 1 || couponVm.Discount > 99)
            {
                throw new BusinessException("Discount should be inclusive between 1 and 99", "couponInvalidDiscount");
            }

            if (_repo.ExistsByCode(couponVm.Code))
            {
                throw new BusinessException($"Coupon with code '{couponVm.Code}' already exists", "couponCodeAlreadyExists", new Dictionary<string, string> { { "code", couponVm.Code } });
            }

            Update(couponVm);
        }

        public void DeleteCouponUsed(int couponId, int couponUsedId)
        {
            var coupon = _repo.GetAll().Where(c => c.Id == couponId && c.CouponUsedId == couponUsedId).FirstOrDefault()
                ?? throw new BusinessException("Given invalid id");
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
            var coupon = _repo.GetByCode(promoCode);
            var couponVm = _mapper.Map<CouponVm>(coupon);
            return couponVm;
        }

        public List<CouponVm> GetAllCouponsNotUsed()
        {
            return _mapper.Map<List<CouponVm>>(_repo.GetNotUsedCoupons());
        }

        public ListForCouponVm GetAllCoupons()
        {
            var coupons = _repo.GetAllCoupons();
            return new ListForCouponVm
            {
                Count = coupons.Count,
                Coupons = _mapper.Map<List<CouponVm>>(coupons),
                CurrentPage = 1,
                PageSize = coupons.Count,
                SearchString = ""
            };
        }

        public CouponVm GetByCouponUsed(int couponUsedId)
        {
            return _mapper.Map<CouponVm>(_repo.GetByCouponUsed(couponUsedId));
        }

        public bool ExistsByCode(string code)
        {
            return _repo.ExistsByCode(code);
        }
    }
}
