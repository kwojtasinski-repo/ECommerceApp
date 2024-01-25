using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Brands
{
    public class BrandService : IBrandService
    {
        private readonly IBrandRepository _brandRepository;
        private readonly IMapper _mapper;

        public BrandService(IBrandRepository brandRepo, IMapper mapper)
        {
            _brandRepository = brandRepo;
            _mapper = mapper;
        }

        public int AddBrand(BrandDto brandDto)
        {
            if (brandDto is null)
            {
                throw new BusinessException($"{typeof(BrandDto).Name} cannot be null");
            }

            if (brandDto.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var entity = _mapper.Map<Brand>(brandDto);
            var id = _brandRepository.AddBrand(entity);
            return id;
        }

        public bool DeleteBrand(int id)
        {
            if (!_brandRepository.ExistsBrand(id))
            {
                return false;
            }

            _brandRepository.DeleteBrand(id);
            return true;
        }

        public ListForBrandVm GetAllBrands(int pageSize, int pageNo, string searchString)
        {
            var brands = _brandRepository.GetAllBrands().Where(it => it.Name.StartsWith(searchString))
                .ProjectTo<BrandDto>(_mapper.ConfigurationProvider)
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

        public BrandDto GetBrand(int id)
        {
            var brand = _brandRepository.GetBrandById(id);
            var brandDto = _mapper.Map<BrandDto>(brand);
            return brandDto;
        }

        public void UpdateBrand(BrandDto brandDto)
        {
            if (brandDto is null)
            {
                throw new BusinessException($"{typeof(BrandDto).Name} cannot be null");
            }

            var entity = _brandRepository.GetBrandById(brandDto.Id);
            entity.Name = brandDto.Name;
            _brandRepository.UpdateBrand(entity);
        }

        public IEnumerable<BrandDto> GetAllBrands(Expression<Func<Brand, bool>> expression)
        {
            var brands = _brandRepository.GetAllBrands().Where(expression)
                .ProjectTo<BrandDto>(_mapper.ConfigurationProvider);
            var brandsToShow = brands.ToList();

            return brandsToShow;
        }

        public bool BrandExists(int id)
        {
            return _brandRepository.ExistsBrand(id);
        }
    }
}
