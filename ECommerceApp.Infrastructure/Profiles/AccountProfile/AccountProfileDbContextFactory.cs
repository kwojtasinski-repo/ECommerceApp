using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile
{
    internal class AccountProfileDbContextFactory : IDesignTimeDbContextFactory<AccountProfileDbContext>
    {
        public AccountProfileDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AccountProfileDbContext>();
            optionsBuilder.UseSqlServer(
                "Server=.;Database=ECommerceApp;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False;");
            return new AccountProfileDbContext(optionsBuilder.Options);
        }
    }
}
