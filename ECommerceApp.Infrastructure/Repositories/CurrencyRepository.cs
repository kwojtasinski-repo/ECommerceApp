using ECommerceApp.Domain.Model;
using ECommerceApp.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq.Expressions;
using System.Linq;
using ECommerceApp.Infrastructure.Database;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CurrencyRepository : GenericRepository<Currency>, ICurrencyRepository
    {
        public CurrencyRepository(Context context) : base(context)
        {
        }

        public List<Currency> GetAll(Expression<Func<Currency, bool>> expression)
        {
            var currencies = _context.Currencies.Where(expression).ToList();
            return currencies;
        }
    }
}
