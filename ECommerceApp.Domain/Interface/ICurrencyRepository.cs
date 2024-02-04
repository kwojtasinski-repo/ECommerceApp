using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface ICurrencyRepository
    {
        int Add(Currency currency);
        bool Delete(Currency currency);
        bool Delete(int id);
        List<Currency> GetAll();
        List<Currency> GetAll(int pageSize, int pageNo, string searchString);
        Currency GetById(int id);
        void Update(Currency currency);
        int GetCountBySearchString(string searchString);
    }
}
