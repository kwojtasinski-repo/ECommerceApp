using ECommerceApp.Domain.Interface;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
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

        public void DeleteType(int typeId)
        {
            var type = _context.Types.Find(typeId);

            if (type != null)
            {
                _context.Types.Remove(type);
                _context.SaveChanges();
            }
        }

        public bool ExistsById(int id)
        {
            return _context.Types.AsNoTracking().Any(type => type.Id == id);
        }

        public IQueryable<Domain.Model.Type> GetAllTypes()
        {
            var types = _context.Types.AsQueryable();
            return types;
        }

        public Domain.Model.Type GetTypeById(int typeId)
        {
            var type = _context.Types.Where(t => t.Id == typeId).FirstOrDefault();
            return type;
        }

        public void UpdateType(Domain.Model.Type type)
        {
            _context.Types.Update(type);
            _context.SaveChanges();
        }
    }
}
