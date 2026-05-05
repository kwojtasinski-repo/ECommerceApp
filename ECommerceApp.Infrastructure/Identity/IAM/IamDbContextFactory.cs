using ECommerceApp.Domain.Identity.IAM;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.Identity.IAM
{
    internal class IamDbContextFactory : IDesignTimeDbContextFactory<IamDbContext>
    {
        public IamDbContext CreateDbContext(string[] args)
        {
            // Build a minimal service provider that mirrors the runtime Identity options
            // so OnModelCreating picks up SchemaVersion = Version2 at design time too.
            var services = new ServiceCollection();
            services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<IamDbContext>();

            var optionsBuilder = new DbContextOptionsBuilder<IamDbContext>();
            optionsBuilder.UseSqlServer(
                "Server=.;Database=ECommerceApp;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False;");
            optionsBuilder.UseApplicationServiceProvider(services.BuildServiceProvider());

            return new IamDbContext(optionsBuilder.Options);
        }
    }
}
