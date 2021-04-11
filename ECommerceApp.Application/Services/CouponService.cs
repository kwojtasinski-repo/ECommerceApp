using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Interfaces;
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
    public class CouponService : CouponServiceAbstract
    {
        private readonly ICouponRepository _couponRepo;
        private readonly IMapper _mapper;

        public CouponService(ICouponRepository couponRepo, IMapper mapper) : base(couponRepo, mapper)
        {
            _couponRepo = couponRepo;
            _mapper = mapper;
        }

        public override int AddCoupon(NewCouponVm couponVm)
        {
            var id = Add(couponVm);
            return id;
        }

        public override int AddCouponType(NewCouponTypeVm couponTypeVm)
        {
            var couponType = _mapper.Map<CouponType>(couponTypeVm);
            var id = _couponRepo.AddCouponType(couponType);
            return id;
        }

        public override int AddCouponUsed(NewCouponUsedVm couponUsedVm)
        {
            var couponUsed = _mapper.Map<CouponUsed>(couponUsedVm);
            var id = _couponRepo.AddCouponUsed(couponUsed);
            return id;
        }

        public override void DeleteCoupon(int id)
        {
            Delete(id);
        }

        public override void DeleteCouponType(int id)
        {
            _couponRepo.DeleteCouponType(id);
        }

        public override void DeleteCouponUsed(int id)
        {
            _couponRepo.DeleteCouponUsed(id);
        }

        public override ListForCouponVm GetAllCoupons(int pageSize, int pageNo, string searchString)
        {
            var coupons = GetAll(searchString);
            var couponsToShow = coupons.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var couponsList = new ListForCouponVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Coupons = couponsToShow,
                Count = coupons.Count
            };

            return couponsList;
        }

        public override ListForCouponTypeVm GetAllCouponsTypes(int pageSize, int pageNo, string searchString)
        {
            var couponTypes = _couponRepo.GetAllCouponsTypes().Where(coupon => coupon.Type.StartsWith(searchString))
                .ProjectTo<CouponTypeForListVm>(_mapper.ConfigurationProvider)
                .ToList();
            var couponTypesToShow = couponTypes.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var couponTypesList = new ListForCouponTypeVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                CouponTypes = couponTypesToShow,
                Count = couponTypes.Count
            };

            return couponTypesList;
        }

        public override IQueryable<NewCouponTypeVm> GetAllCouponsTypes()
        {
            var couponTypes = _couponRepo.GetAllCouponsTypes();
            var couponTypesVm = couponTypes.ProjectTo<NewCouponTypeVm>(_mapper.ConfigurationProvider);
            return couponTypesVm;
        }

        public override ListForCouponUsedVm GetAllCouponsUsed(int pageSize, int pageNo, string searchString)
        {
            var couponsUsed = _couponRepo.GetAllCouponsUsed()//.Where(coupon => coupon..StartsWith(searchString))
                .ProjectTo<CouponUsedForListVm>(_mapper.ConfigurationProvider)
                .ToList();
            var couponsUsedToShow = couponsUsed.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var couponsUsedList = new ListForCouponUsedVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                CouponsUsed = couponsUsedToShow,
                Count = couponsUsed.Count
            };

            return couponsUsedList;
        }

        public override IQueryable<NewCouponUsedVm> GetAllCouponsUsed()
        {
            var couponsUsed = _couponRepo.GetAllCouponsUsed();
            var couponsUsedVm = couponsUsed.ProjectTo<NewCouponUsedVm>(_mapper.ConfigurationProvider);
            return couponsUsedVm;
        }

        public override IQueryable<NewOrderVm> GetAllOrders()
        {
            var orders = _couponRepo.GetAllOrders();
            //var ordersVm = _mapper.Map<List<NewOrderVm>(orders);
            var ordersVm = orders.ProjectTo<NewOrderVm>(_mapper.ConfigurationProvider);
            return ordersVm;
        }

        public override CouponDetailsVm GetCouponDetail(int id)
        {
            var couponDetails = _couponRepo.GetCouponById(id);
            var couponDetailsVm = _mapper.Map<CouponDetailsVm>(couponDetails);
            return couponDetailsVm;
        }

        public override NewCouponVm GetCouponForEdit(int id)
        {
            var couponVm = Get(id);
            return couponVm;
        }

        public override CouponTypeDetailsVm GetCouponTypeDetail(int id)
        {
            var couponType = _couponRepo.GetCouponTypeById(id);
            var couponTypeVm = _mapper.Map<CouponTypeDetailsVm>(couponType);
            return couponTypeVm;
        }

        public override NewCouponTypeVm GetCouponTypeForEdit(int id)
        {
            var couponType = _couponRepo.GetCouponTypeById(id);
            var couponTypeVm = _mapper.Map<NewCouponTypeVm>(couponType);
            return couponTypeVm;
        }

        public override CouponUsedDetailsVm GetCouponUsedDetail(int id)
        {
            var couponUsed = _couponRepo.GetCouponUsedById(id);
            var couponUsedVm = _mapper.Map<CouponUsedDetailsVm>(couponUsed);
            return couponUsedVm;
        }

        public override NewCouponTypeVm GetCouponUsedForEdit(int id)
        {
            var couponUsed = _couponRepo.GetCouponUsedById(id);
            var couponUsedVm = _mapper.Map<NewCouponTypeVm>(couponUsed);
            return couponUsedVm;
        }

        public override void UpdateCoupon(NewCouponVm couponVm)
        {
            Update(couponVm);
        }

        public override void UpdateCouponType(NewCouponTypeVm couponTypeVm)
        {
            var couponType = _mapper.Map<CouponType>(couponTypeVm);
            _couponRepo.UpdateCouponType(couponType);
        }

        public override void UpdateCouponUsed(NewCouponUsedVm couponUsedVm)
        {
            var couponUsed = _mapper.Map<CouponUsed>(couponUsedVm);
            _couponRepo.UpdateCouponUsed(couponUsed);
        }
    }
}
