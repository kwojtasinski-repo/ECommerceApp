using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Brand;
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
    public class BrandService : AbstractService<BrandVm, IBrandRepository, Brand>, IBrandService
    {
        public BrandService(IBrandRepository brandRepo, IMapper mapper) : base(brandRepo, mapper)
        { }

        public int AddBrand(BrandVm brandVm)
        {
            if (brandVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }
            var id = Add(brandVm);
            return id;
        }

        public void DeleteBrand(int id)
        {
            Delete(id);
        }

        public ListForBrandVm GetAllBrands(int pageSize, int pageNo, string searchString)
        {
            var brands = _repo.GetAllBrands().Where(it => it.Name.StartsWith(searchString))
                .ProjectTo<BrandVm>(_mapper.ConfigurationProvider)
                .ToList();
            var brandsToShow = brands.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var brandsList = new ListForBrandVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Brands = brandsToShow,
                Count = brands.Count
            };

            return brandsList;
        }

        public BrandDetailsVm GetBrandDetail(int id)
        {
            var brand = _repo.GetAll().Include(i => i.Items).Where(b => b.Id == id).FirstOrDefault();
            var brandDetails = _mapper.Map<BrandDetailsVm>(brand);
            return brandDetails;
        }

        public BrandVm GetBrand(int id)
        {
            var brand = _repo.GetBrandById(id);
            var brandVm = _mapper.Map<BrandVm>(brand);
            return brandVm;
        }

        public void UpdateBrand(BrandVm brandVm)
        {
            if (brandVm != null)
            {
                Update(brandVm);
            }
        }

        public IEnumerable<BrandVm> GetAllBrands(Expression<Func<Brand, bool>> expression)
        {
            var brands = _repo.GetAllBrands().Where(expression)
                .ProjectTo<BrandVm>(_mapper.ConfigurationProvider);
            var brandsToShow = brands.ToList();

            return brandsToShow;
        }

        public bool BrandExists(int id)
        {
            var brand = _repo.GetById(id);
            var exists = brand != null;

            if (exists)
            {
                _repo.DetachEntity(brand);
            }

            return exists;
        }
    }
}
