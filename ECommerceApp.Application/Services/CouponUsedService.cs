using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
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

namespace ECommerceApp.Application.Services
{
    public class CouponUsedService : AbstractService<CouponUsedVm, ICouponUsedRepository, CouponUsed>, ICouponUsedService
    {
        private readonly IOrderService _orderService;
        private readonly ICouponService _couponService;

        public CouponUsedService(ICouponUsedRepository couponRepo, IMapper mapper, IOrderService orderService, ICouponService couponService) : base(couponRepo, mapper)
        {
            _orderService = orderService;
            _couponService = couponService;
        }

        public int AddCouponUsed(CouponUsedVm couponUsedVm)
        {
            if (couponUsedVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var couponUsed = _mapper.Map<CouponUsed>(couponUsedVm);
            var id = _repo.AddCouponUsed(couponUsed);
            _orderService.AddCouponToOrder(couponUsed.OrderId, id);
            _couponService.AddCouponUsed(couponUsed.CouponId, id);
            return id;
        }
        public void DeleteCouponUsed(int id)
        {
            var coupon = _repo.GetAll().Where(cu => cu.Id == id).FirstOrDefault();

            if (coupon is null)
            {
                throw new BusinessException("Given invalid id");
            }

            _repo.DetachEntity(coupon);

            _orderService.DeleteCouponUsed(coupon.OrderId, coupon.Id);
            _couponService.DeleteCouponUsed(coupon.CouponId, coupon.Id);

            _repo.Delete(id);
        }

        public ListForCouponUsedVm GetAllCouponsUsed(int pageSize, int pageNo, string searchString)
        {
            var couponsUsed = _repo.GetAllCouponsUsed()//.Where(coupon => coupon..StartsWith(searchString))
                .ProjectTo<CouponUsedVm>(_mapper.ConfigurationProvider)
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

        public IQueryable<CouponUsedVm> GetAllCouponsUsed()
        {
            var couponsUsed = _repo.GetAllCouponsUsed();
            var couponsUsedVm = couponsUsed.ProjectTo<CouponUsedVm>(_mapper.ConfigurationProvider);
            return couponsUsedVm;
        }

        public CouponUsedDetailsVm GetCouponUsedDetail(int id)
        {
            var couponUsed = _repo.GetAll()
                .Include(c => c.Coupon)
                .Include(o => o.Order)
                .Where(cu => cu.Id == id)
                .FirstOrDefault();
            var couponUsedVm = _mapper.Map<CouponUsedDetailsVm>(couponUsed);
            return couponUsedVm;
        }

        public CouponUsedVm GetCouponUsed(int id)
        {
            var couponUsed = _repo.GetCouponUsedById(id);
            var couponUsedVm = _mapper.Map<CouponUsedVm>(couponUsed);
            return couponUsedVm;
        }

        public void UpdateCouponUsed(CouponUsedVm couponUsedVm)
        {
            var couponUsed = _mapper.Map<CouponUsed>(couponUsedVm);
            _repo.UpdateCouponUsed(couponUsed);
        }

        public IEnumerable<CouponVm> GetAllCouponsUsed(Expression<Func<CouponUsed, bool>> expression)
        {
            var coupons = _repo.GetAll().Where(expression)
                .ProjectTo<CouponVm>(_mapper.ConfigurationProvider);
            var couponsToShow = coupons.ToList();

            return couponsToShow;
        }
    }
}
