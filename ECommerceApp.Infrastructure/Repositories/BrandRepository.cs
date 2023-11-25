using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class BrandRepository : GenericRepository<Brand>, IBrandRepository
    {
        public BrandRepository(Context context) : base(context)
        {
        }

        public int AddBrand(Brand brand)
        {
            _context.Brands.Add(brand);
            _context.SaveChanges();
            return brand.Id;
        }

        public void DeleteBrand(int brandId)
        {
            var brand = _context.Brands.Find(brandId);

            if (brand != null)
            {
                _context.Brands.Remove(brand);
                _context.SaveChanges();
            }
        }

        public bool ExistsBrand(int id)
        {
            return _context.Brands
                .AsNoTracking()
                .Any(b => b.Id == id);
        }

        public IQueryable<Brand> GetAllBrands()
        {
            var brands = _context.Brands.AsQueryable();
            return brands;
        }

        public Brand GetBrandById(int brandId)
        {
            var brand = _context.Brands.FirstOrDefault(b => b.Id == brandId);
            return brand;
        }

        public void UpdateBrand(Brand brand)
        {
            _context.Brands.Update(brand);
            _context.SaveChanges();
        }
    }
}
