using ECommerceApp.Domain.Supporting.Verification;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Supporting.Verification
{
    internal sealed class VerificationDbContext : DbContext, IVerificationDbContext
    {
        public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();

        public VerificationDbContext(DbContextOptions<VerificationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema(VerificationConstants.SchemaName);
            builder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Supporting.Verification.Configurations"));
            builder.UseUtcDateTimes();
        }
    }
}