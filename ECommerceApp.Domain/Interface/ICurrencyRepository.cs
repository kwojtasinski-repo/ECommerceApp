using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ICurrencyRepository : IGenericRepository<Currency>
    {
        List<Currency> GetAll(Expression<Func<Currency, bool>> expression);
    }
}
