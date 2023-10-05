using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

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

        public IQueryable<Brand> GetAllBrands()
        {
            var brands = _context.Brands.AsQueryable();
            return brands;
        }

        public Brand GetBrandById(int brandId)
        {
            var brand = _context.Brands.Where(b => b.Id == brandId).FirstOrDefault();
            return brand;
        }

        public void UpdateBrand(Brand brand)
        {
            _context.Brands.Update(brand);
            _context.SaveChanges();
        }
    }
}
