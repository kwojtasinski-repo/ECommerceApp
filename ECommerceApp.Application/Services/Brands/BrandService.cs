using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;

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
            return _brandRepository.DeleteBrand(id);
        }

        public ListForBrandVm GetAllBrands(int pageSize, int pageNo, string searchString)
        {
            var brands = _brandRepository.GetAllBrands(searchString, pageSize, pageNo);
            var brandsCount = _brandRepository.GetCountBySearchString(searchString);

            var brandsList = new ListForBrandVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Brands = _mapper.Map<List<BrandDto>>(brands),
                Count = brandsCount
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

        public IEnumerable<BrandDto> GetAllBrands()
        {
            return _mapper.Map<List<BrandDto>>(_brandRepository.GetAllBrands());
        }

        public bool BrandExists(int id)
        {
            return _brandRepository.ExistsBrand(id);
        }
    }
}
