using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public int Add(T entity)
        {
            _context.Add(entity);
            _context.SaveChanges();
            return entity.Id;
        }
        
        public async Task<int> AddAsync(T entity)
        {
            _context.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }
        public List<int> AddRange(List<T> entities)
        {
            _context.AddRange(entities);
            _context.SaveChanges();

            var ids = new List<int>();
            foreach (var entity in entities)
            {
                ids.Add(entity.Id);
            }

            return ids;
        }
        
        public async Task<List<int>> AddRangeAsync(List<T> entities)
        {
            _context.Add(entities);
            await _context.SaveChangesAsync();

            var ids = new List<int>();
            foreach(var entity in entities)
            {
                ids.Add(entity.Id);
            }

            return ids;
        }

        public async Task DeleteAsync(int id)
        {
            var type = typeof(T);
            var entity = (T) await _context.FindAsync(type, id);

            if (entity != null)
            {
                _context.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
        
        public void Delete(int id)
        {
            var type = typeof(T);
            var entity = (T) _context.Find(type, id);

            if (entity != null)
            {
                _context.Remove(entity);
                _context.SaveChanges();
            }
        }

        public IQueryable<T> GetAll()
        {
            var dbSet = _context.Set<T>();
            var queryable = dbSet.AsQueryable();
            return queryable;
        }

        public async Task<T> GetByIdAsync(int id)
        {
            var entity = (T) await _context.FindAsync(typeof(T), id);
            return entity;
        }
        
        public T GetById(int id)
        {
            var entity = (T) _context.Find(typeof(T), id);
            return entity;
        }

        protected Microsoft.EntityFrameworkCore.DbSet<T> GetDbSet()
        {
            var dbSet = _context.Set<T>();
            return dbSet;
        }

        public async Task UpdateAsync(T entity)
        {
            if (entity != null)
            {
                _context.Update(entity);
                await _context.SaveChangesAsync();
            }
        }

        public void Update(T entity)
        {
            if (entity != null)
            {
                _context.Update(entity);
                _context.SaveChanges();
            }
        }

        public async Task DeleteAsync(T entity)
        {
            if (entity != null)
            {
                _context.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public void Delete(T entity)
        {
            if (entity != null)
            {
                _context.Remove(entity);
                _context.SaveChanges();
            }
        }

        public void DetachEntity(T entity)
        {
            _context.Entry(entity).State = EntityState.Detached;
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

        public void UpdateRange(IEnumerable<T> entities)
        {
            if (entities.Count() == 0)
            {
                return;
            }

            _context.UpdateRange(entities);
            _context.SaveChanges();
        }
    }
}
