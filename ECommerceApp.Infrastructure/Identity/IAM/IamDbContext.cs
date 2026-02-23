using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Identity.IAM
{
    public class IamDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public IamDbContext(DbContextOptions<IamDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema(IamConstants.Schema);
            builder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace == "ECommerceApp.Infrastructure.Identity.IAM.Configurations");
        }
    }
}
