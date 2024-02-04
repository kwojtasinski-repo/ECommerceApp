using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class TypeRepository : ITypeRepository
    {
        private readonly Context _context;

        public TypeRepository(Context context)
        {
            _context = context;
        }

        public int AddType(Domain.Model.Type type)
        {
            _context.Types.Add(type);
            _context.SaveChanges();
            return type.Id;
        }

        public bool DeleteType(int typeId)
        {
            var type = _context.Types.Find(typeId);
            if (type is null)
            {
                return false;
            }

            _context.Types.Remove(type);
            return _context.SaveChanges() > 0;
        }

        public bool ExistsById(int id)
        {
            return _context.Types.AsNoTracking().Any(type => type.Id == id);
        }

        public List<Domain.Model.Type> GetAllTypes()
        {
            return _context.Types.ToList();
        }

        public List<Type> GetAllTypes(int pageSize, int pageNo, string searchString)
        {
            return _context.Types
                           .Where(it => it.Name.StartsWith(searchString))
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.Types
                           .Where(it => it.Name.StartsWith(searchString))
                           .Count();
        }

        public Domain.Model.Type GetTypeById(int typeId)
        {
            var type = _context.Types.Where(t => t.Id == typeId).FirstOrDefault();
            return type;
        }

        public Type GetTypeDetailsById(int typeId)
        {
            return _context.Types
                           .Include(t => t.Items)
                           .FirstOrDefault(t => t.Id == typeId);
        }

        public void UpdateType(Domain.Model.Type type)
        {
            _context.Types.Update(type);
            _context.SaveChanges();
        }
    }
}
