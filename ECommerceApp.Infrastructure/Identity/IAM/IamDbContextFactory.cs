using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.Identity.IAM
{
    internal class IamDbContextFactory : IDesignTimeDbContextFactory<IamDbContext>
    {
        public IamDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<IamDbContext>();
            optionsBuilder.UseSqlServer(
                "Server=.;Database=ECommerceApp;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False;");
            return new IamDbContext(optionsBuilder.Options);
        }
    }
}
