using AutoMapper;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.CouponType;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Coupons
{
    public class CouponTypeService : AbstractService<CouponTypeVm, ICouponTypeRepository, CouponType>, ICouponTypeService
    {
        public CouponTypeService(ICouponTypeRepository couponRepo, IMapper mapper) : base(couponRepo, mapper)
        {
        }

        public int AddCouponType(CouponTypeVm couponTypeVm)
        {
            if (couponTypeVm is null)
            {
                throw new BusinessException($"{typeof(CouponTypeVm).Name} cannot be null");
            }

            if (couponTypeVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var couponType = _mapper.Map<CouponType>(couponTypeVm);
            var id = _repo.AddCouponType(couponType);
            return id;
        }

        public void DeleteCouponType(int id)
        {
            _repo.DeleteCouponType(id);
        }

        public ListForCouponTypeVm GetAllCouponsTypes(int pageSize, int pageNo, string searchString)
        {
            var couponTypes = _mapper.Map<List<CouponTypeVm>>(_repo.GetAllCouponTypes(pageSize, pageNo, searchString));
            
            var couponTypesList = new ListForCouponTypeVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                CouponTypes = couponTypes,
                Count = _repo.GetCountBySearchString(searchString)
            };

            return couponTypesList;
        }

        public CouponTypeDetailsVm GetCouponTypeDetail(int id)
        {
            var couponType = _repo.GetCouponTypeById(id);
            var couponTypeVm = _mapper.Map<CouponTypeDetailsVm>(couponType);
            return couponTypeVm;
        }

        public CouponTypeVm GetCouponType(int id)
        {
            var couponType = _repo.GetCouponTypeById(id);
            var couponTypeVm = _mapper.Map<CouponTypeVm>(couponType);
            return couponTypeVm;
        }

        public void UpdateCouponType(CouponTypeVm couponTypeVm)
        {
            if (couponTypeVm is null)
            {
                throw new BusinessException($"{typeof(CouponTypeVm).Name} cannot be null");
            }

            var couponType = _mapper.Map<CouponType>(couponTypeVm);
            _repo.UpdateCouponType(couponType);
        }

        public IEnumerable<CouponTypeVm> GetAllCouponsTypes()
        {
            return _mapper.Map<List<CouponTypeVm>>(_repo.GetAllCouponTypes());
        }
    }
}
