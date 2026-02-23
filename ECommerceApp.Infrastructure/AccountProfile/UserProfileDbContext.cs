using ECommerceApp.Domain.AccountProfile;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.AccountProfile
{
    public class UserProfileDbContext : DbContext
    {
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

        public UserProfileDbContext(DbContextOptions<UserProfileDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace == "ECommerceApp.Infrastructure.AccountProfile.Configurations");
        }
    }
}
