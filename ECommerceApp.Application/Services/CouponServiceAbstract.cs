using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public abstract class CouponServiceAbstract : IBaseService<NewCouponVm>
    {
        private readonly ICouponRepository _couponRepo;
        private readonly IMapper _mapper;

        public CouponServiceAbstract(ICouponRepository couponRepo, IMapper mapper)
        {
            _couponRepo = couponRepo;
            _mapper = mapper;
        }

        public int Add(NewCouponVm objectVm)
        {
            if (objectVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            int id = AddCoupon(objectVm);
            return id;
        }

        public void Delete(int id)
        {
            DeleteCoupon(id);
        }

        public NewCouponVm Get(int id)
        {
            var coupon = _couponRepo.GetCouponById(id);
            var couponVm = _mapper.Map<NewCouponVm>(coupon);
            return couponVm;
        }

        public List<NewCouponVm> GetAll()
        {
            var coupons = _couponRepo.GetAllCoupons().ProjectTo<NewCouponVm>(_mapper.ConfigurationProvider).ToList();
            return coupons;
        }

        public List<NewCouponVm> GetAll(string searchString)
        {
            var coupons = _couponRepo.GetAllCoupons().Where(coupon => coupon.Code.StartsWith(searchString))
                .ProjectTo<NewCouponVm>(_mapper.ConfigurationProvider)
                .ToList();
            return coupons;
        }

        public void Update(NewCouponVm objectVm)
        {
            var coupon = _mapper.Map<Coupon>(objectVm);
            _couponRepo.UpdateCoupon(coupon);
        }

        public abstract int AddCoupon(NewCouponVm couponVm);

        public abstract int AddCouponType(NewCouponTypeVm couponTypeVm);

        public abstract int AddCouponUsed(NewCouponUsedVm couponUsedVm);

        public abstract void DeleteCoupon(int id);

        public abstract void DeleteCouponType(int id);

        public abstract void DeleteCouponUsed(int id);

        public abstract ListForCouponVm GetAllCoupons(int pageSize, int pageNo, string searchString);

        public abstract ListForCouponTypeVm GetAllCouponsTypes(int pageSize, int pageNo, string searchString);

        public abstract IQueryable<NewCouponTypeVm> GetAllCouponsTypes();

        public abstract ListForCouponUsedVm GetAllCouponsUsed(int pageSize, int pageNo, string searchString);

        public abstract IQueryable<NewCouponUsedVm> GetAllCouponsUsed();

        public abstract IQueryable<NewOrderVm> GetAllOrders();

        public abstract CouponDetailsVm GetCouponDetail(int id);

        public abstract NewCouponVm GetCouponForEdit(int id);

        public abstract CouponTypeDetailsVm GetCouponTypeDetail(int id);

        public abstract NewCouponTypeVm GetCouponTypeForEdit(int id);

        public abstract CouponUsedDetailsVm GetCouponUsedDetail(int id);

        public abstract NewCouponTypeVm GetCouponUsedForEdit(int id);

        public abstract void UpdateCoupon(NewCouponVm couponVm);

        public abstract void UpdateCouponType(NewCouponTypeVm couponTypeVm);

        public abstract void UpdateCouponUsed(NewCouponUsedVm couponUsedVm);
    }
}
