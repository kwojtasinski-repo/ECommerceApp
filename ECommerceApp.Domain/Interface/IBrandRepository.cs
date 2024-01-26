using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IBrandRepository
    {
        bool DeleteBrand(int brandId);
        int AddBrand(Brand brand);
        Brand GetBrandById(int brandId);
        List<Brand> GetAllBrands();
        List<Brand> GetAllBrands(string searchString, int pageSize, int pageNo);
        void UpdateBrand(Brand brand);
        bool ExistsBrand(int id);
        int GetCountBySearchString(string searchString);
    }
}
