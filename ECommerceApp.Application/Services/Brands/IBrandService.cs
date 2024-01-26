using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Brand;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Brands
{
    public interface IBrandService
    {
        int AddBrand(BrandDto brandDto);
        bool DeleteBrand(int id);
        ListForBrandVm GetAllBrands(int pageSize, int pageNo, string searchString);
        BrandDto GetBrand(int id);
        void UpdateBrand(BrandDto brandDto);
        IEnumerable<BrandDto> GetAllBrands();
        bool BrandExists(int id);
    }
}
