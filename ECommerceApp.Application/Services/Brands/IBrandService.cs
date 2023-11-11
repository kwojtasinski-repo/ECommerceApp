using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Brands
{
    public interface IBrandService : IAbstractService<BrandVm, IBrandRepository, Brand>
    {
        int AddBrand(BrandVm brandVm);
        void DeleteBrand(int id);
        ListForBrandVm GetAllBrands(int pageSize, int pageNo, string searchString);
        BrandDetailsVm GetBrandDetail(int id);
        BrandVm GetBrand(int id);
        void UpdateBrand(BrandVm brandVm);
        IEnumerable<BrandVm> GetAllBrands(Expression<Func<Brand, bool>> expression);
        bool BrandExists(int id);
    }
}
