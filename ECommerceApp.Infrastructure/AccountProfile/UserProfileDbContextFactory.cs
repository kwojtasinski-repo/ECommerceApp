using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.AccountProfile
{
    internal class UserProfileDbContextFactory : IDesignTimeDbContextFactory<UserProfileDbContext>
    {
        public UserProfileDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UserProfileDbContext>();
            optionsBuilder.UseSqlServer(
                "Server=.;Database=ECommerceApp;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False;");
            return new UserProfileDbContext(optionsBuilder.Options);
        }
    }
}
