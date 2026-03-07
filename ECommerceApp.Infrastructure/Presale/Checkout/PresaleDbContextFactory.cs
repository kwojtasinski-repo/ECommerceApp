using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Presale.Checkout
{
    internal sealed class PresaleDbContextFactory : IDesignTimeDbContextFactory<PresaleDbContext>
    {
        public PresaleDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PresaleDbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new PresaleDbContext(optionsBuilder.Options);
        }
    }
}
