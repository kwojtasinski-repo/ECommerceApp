using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class BrandRepository : IBrandRepository
    {
        private readonly Context _context;

        public BrandRepository(Context context)
        {
            _context = context;
        }

        public int AddBrand(Brand brand)
        {
            _context.Brands.Add(brand);
            _context.SaveChanges();
            return brand.Id;
        }

        public bool DeleteBrand(int brandId)
        {
            var brand = _context.Brands.Find(brandId);
            if (brand is null)
            {
                return false;
            }

            _context.Brands.Remove(brand);
            return _context.SaveChanges() > 0;
        }

        public bool ExistsBrand(int id)
        {
            return _context.Brands
                .AsNoTracking()
                .Any(b => b.Id == id);
        }

        public List<Brand> GetAllBrands()
        {
            return _context.Brands.ToList();
        }

        public List<Brand> GetAllBrands(string searchString, int pageSize, int pageNo)
        {
            return _context.Brands
                    .Where(it => it.Name.StartsWith(searchString))
                    .Skip(pageSize * (pageNo - 1))
                    .Take(pageSize)
                    .ToList();
        }

        public Brand GetBrandById(int brandId)
        {
            var brand = _context.Brands.FirstOrDefault(b => b.Id == brandId);
            return brand;
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.Brands
                    .Where(it => it.Name.StartsWith(searchString))
                    .Count();
        }

        public void UpdateBrand(Brand brand)
        {
            _context.Brands.Update(brand);
            _context.SaveChanges();
        }
    }
}
