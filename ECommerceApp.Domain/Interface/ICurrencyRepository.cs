using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface ICurrencyRepository
    {
        int Add(Currency currency);
        bool Delete(Currency currency);
        List<Currency> GetAll();
        List<Currency> GetAll(int pageSize, int pageNo, string searchString);
        Currency GetById(int id);
        void Update(Currency currency);
        int GetCountBySearchString(string searchString);
    }
}
