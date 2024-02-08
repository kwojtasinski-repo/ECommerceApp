using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.UnitTests.Common
{
    internal class GenericInMemoryRepository<T>
        where T : BaseEntity
    {
        private readonly List<T> _list = new();

        public int Add(T item)
        {
            var id = (_list.LastOrDefault()?.Id ?? 0) + 1;
            item.Id = id;
            _list.Add(item);
            return id;
        }
        
        public void Update(T item)
        {
            var element = _list.FirstOrDefault(o => o.Id == item.Id);
            if (element is null)
            {
                return;
            }

            _list.Remove(element);
            _list.Add(item);
        }

        public bool Delete(T item)
        {
            var element = _list.FirstOrDefault(o => o.Id == item.Id);
            if (element is null)
            {
                return false;
            }

            _list.Remove(element);
            return true;
        }

        public bool Delete(int id)
        {
            var element = _list.FirstOrDefault(o => o.Id == id);
            if (element is null)
            {
                return false;
            }

            _list.Remove(element);
            return true;
        }

        public T GetById(int id)
        {
            return _list.FirstOrDefault(o => o.Id == id);
        }

        public IEnumerable<T> GetAll()
        {
            return _list;
        }
    }
}
