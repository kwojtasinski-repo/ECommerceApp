using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Interface
{
    public interface IAbstractRepository<T>
    {
        Task DeleteAsync(int id);
        void Delete(int id);
        Task DeleteAsync(T entity);
        void Delete(T entity);
        Task<int> AddAsync(T entity);
        int Add(T entity);
        Task<T> GetByIdAsync(int id);
        T GetById(int id);
        Task UpdateAsync(T entity);
        void Update(T entity);
        IQueryable<T> GetAll();
        Task<List<int>> AddRangeAsync(List<T> entities);
        List<int> AddRange(List<T> entities);
    }
}
