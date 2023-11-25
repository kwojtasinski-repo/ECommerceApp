using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Brands
{
    public interface IBrandService
    {
        int AddBrand(BrandDto brandDto);
        bool DeleteBrand(int id);
        ListForBrandVm GetAllBrands(int pageSize, int pageNo, string searchString);
        BrandDto GetBrand(int id);
        void UpdateBrand(BrandDto brandDto);
        IEnumerable<BrandDto> GetAllBrands(Expression<Func<Brand, bool>> expression);
        bool BrandExists(int id);
    }
}
