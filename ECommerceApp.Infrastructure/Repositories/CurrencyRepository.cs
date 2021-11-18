using ECommerceApp.Domain.Model;
using ECommerceApp.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CurrencyRepository : GenericRepository<Currency>, ICurrencyRepository
    {
        public CurrencyRepository(Context context) : base(context)
        {
        }
    }
}
