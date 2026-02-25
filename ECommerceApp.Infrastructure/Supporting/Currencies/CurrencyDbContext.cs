using ECommerceApp.Domain.Supporting.Currencies;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Supporting.Currencies
{
    public class CurrencyDbContext : DbContext
    {
        public DbSet<Currency> Currencies => Set<Currency>();
        public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();

        public CurrencyDbContext(DbContextOptions<CurrencyDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema(CurrencyConstants.SchemaName);
            builder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Supporting.Currencies.Configurations"));
        }
    }
}
