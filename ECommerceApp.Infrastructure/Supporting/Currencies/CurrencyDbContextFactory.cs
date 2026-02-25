using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Supporting.Currencies
{
    internal sealed class CurrencyDbContextFactory : IDesignTimeDbContextFactory<CurrencyDbContext>
    {
        public CurrencyDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CurrencyDbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new CurrencyDbContext(optionsBuilder.Options);
        }
    }
}
