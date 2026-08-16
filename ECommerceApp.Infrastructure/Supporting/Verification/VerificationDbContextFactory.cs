using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Supporting.Verification
{
    internal sealed class VerificationDbContextFactory : IDesignTimeDbContextFactory<VerificationDbContext>
    {
        public VerificationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<VerificationDbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new VerificationDbContext(optionsBuilder.Options);
        }
    }
}