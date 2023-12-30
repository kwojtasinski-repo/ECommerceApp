using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Interface
{
    public interface IGenericRepository<T>
    {
        Task<bool> DeleteAsync(int id);
        bool Delete(int id);
        Task<bool> DeleteAsync(T entity);
        bool Delete(T entity);
        Task<int> AddAsync(T entity);
        int Add(T entity);
        Task<T> GetByIdAsync(int id);
        T GetById(int id);
        Task UpdateAsync(T entity);
        void Update(T entity);
        void UpdateRange(IEnumerable<T> entities);
        IQueryable<T> GetAll();
        Task<List<int>> AddRangeAsync(IEnumerable<T> entities);
        List<int> AddRange(IEnumerable<T> entities);
        void DetachEntity(T entity);
        void DetachEntity<TEntity>(TEntity entity) where TEntity : BaseEntity;
        void DetachEntity<TEntity>(ICollection<TEntity> entity) where TEntity : BaseEntity;
    }
}
