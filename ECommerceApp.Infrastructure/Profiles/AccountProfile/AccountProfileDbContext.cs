using Microsoft.EntityFrameworkCore;
using AP = ECommerceApp.Domain.Profiles.AccountProfile;

namespace ECommerceApp.Infrastructure.Profiles.AccountProfile
{
    public class AccountProfileDbContext : DbContext
    {
        public DbSet<AP.AccountProfile> AccountProfiles => Set<AP.AccountProfile>();
        public DbSet<AP.Address> Addresses => Set<AP.Address>();
        public DbSet<AP.ContactDetail> ContactDetails => Set<AP.ContactDetail>();
        public DbSet<AP.ContactDetailType> ContactDetailTypes => Set<AP.ContactDetailType>();

        public AccountProfileDbContext(DbContextOptions<AccountProfileDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace == "ECommerceApp.Infrastructure.Profiles.AccountProfile.Configurations");
        }
    }
}
