using ECommerceApp.Domain.Identity.IAM;
using ECommerceApp.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Identity.IAM
{
    public class IamDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public DbSet<RefreshToken> RefreshTokens { get; set; }

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
            builder.UseUtcDateTimes();
            SeedRoles(builder);
        }

        private static void SeedRoles(ModelBuilder builder)
        {
            builder.Entity<IdentityRole>().HasData(
                new IdentityRole { Id = "Administrator", Name = "Administrator", NormalizedName = "ADMINISTRATOR" },
                new IdentityRole { Id = "Manager", Name = "Manager", NormalizedName = "MANAGER" },
                new IdentityRole { Id = "Service", Name = "Service", NormalizedName = "SERVICE" },
                new IdentityRole { Id = "User", Name = "User", NormalizedName = "USER" },
                new IdentityRole { Id = "NotRegister", Name = "NotRegister", NormalizedName = "NOTREGISTER" });
        }
    }
}
