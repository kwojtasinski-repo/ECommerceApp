using ECommerceApp.Domain.Supporting.Currencies;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.Currencies
{
    internal interface ICurrencyDbContext
    {
        DbSet<Currency> Currencies { get; }
        DbSet<CurrencyRate> CurrencyRates { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
