using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
    {
        protected readonly Context _context;

        public GenericRepository(Context context)
        {
            _context = context;
        }

        public virtual int Add(T entity)
        {
            GetDbSet().Add(entity);
            _context.SaveChanges();
            return entity.Id;
        }
        
        public virtual async Task<int> AddAsync(T entity)
        {
            await GetDbSet().AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }

        public virtual List<int> AddRange(IEnumerable<T> entities)
        {
            GetDbSet().AddRange(entities);
            _context.SaveChanges();

            var ids = new List<int>();
            foreach (var entity in entities)
            {
                ids.Add(entity.Id);
            }

            return ids;
        }
        
        public virtual async Task<List<int>> AddRangeAsync(IEnumerable<T> entities)
        {
            await GetDbSet().AddRangeAsync(entities);
            await _context.SaveChangesAsync();

            var ids = new List<int>();
            foreach(var entity in entities)
            {
                ids.Add(entity.Id);
            }

            return ids;
        }

        public virtual async Task<bool> DeleteAsync(int id)
        {
            return (await GetDbSet().Where(e => e.Id == id).ExecuteDeleteAsync()) > 0;
        }
        
        public virtual bool Delete(int id)
        {
            return GetDbSet().Where(e => e.Id == id).ExecuteDelete() > 0;
        }

        public IQueryable<T> GetAll()
        {
            var dbSet = _context.Set<T>();
            var queryable = dbSet.AsQueryable();
            return queryable;
        }

        public virtual async Task<T> GetByIdAsync(int id)
        {
            var entity = await GetDbSet().FindAsync(id);
            return entity;
        }
        
        public virtual T GetById(int id)
        {
            var entity = GetDbSet().Find(id);
            return entity;
        }

        public virtual async Task UpdateAsync(T entity)
        {
            if (entity != null)
            {
                GetDbSet().Update(entity);
                await _context.SaveChangesAsync();
            }
        }

        public virtual void Update(T entity)
        {
            if (entity != null)
            {
                GetDbSet().Update(entity);
                _context.SaveChanges();
            }
        }

        public virtual async Task<bool> DeleteAsync(T entity)
        {
            GetDbSet().Remove(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public virtual bool Delete(T entity)
        {
            GetDbSet().Remove(entity);
            return _context.SaveChanges() > 0;
        }

        public void DetachEntity(T entity)
        {
            GetDbSet().Entry(entity).State = EntityState.Detached;
        }

        public void DetachEntity<TEntity>(TEntity entity) where TEntity : BaseEntity
        {
            _context.Entry(entity).State = EntityState.Detached;
        }

        public void DetachEntity<TEntity>(ICollection<TEntity> entities) where TEntity : BaseEntity
        {
            foreach(var entity in entities)
            {
                _context.Entry(entity).State = EntityState.Detached;
            }
        }

        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            if (!entities.Any())
            {
                return;
            }

            GetDbSet().UpdateRange(entities);
            _context.SaveChanges();
        }

        protected DbSet<T> GetDbSet()
        {
            return _context.Set<T>();
        }
    }
}
