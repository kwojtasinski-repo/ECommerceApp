using ECommerceApp.Domain.Model;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface IBrandRepository : IGenericRepository<Brand>
    {
        void DeleteBrand(int brandId);
        int AddBrand(Brand brand);
        Brand GetBrandById(int brandId);
        IQueryable<Brand> GetAllBrands();
        void UpdateBrand(Brand brand);
    }
}
