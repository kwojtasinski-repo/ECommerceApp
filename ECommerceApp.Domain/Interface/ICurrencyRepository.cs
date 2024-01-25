using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Domain.Interface
{
    public interface ICurrencyRepository
    {
        int Add(Currency currency);
        bool Delete(Currency currency);
        List<Currency> GetAll(Expression<Func<Currency, bool>> expression);
        IQueryable<Currency> GetAll();
        Currency GetById(int id);
        void Update(Currency currency);
    }
}
