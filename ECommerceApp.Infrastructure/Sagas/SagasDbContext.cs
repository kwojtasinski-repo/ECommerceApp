using ECommerceApp.Domain.Sagas;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sagas
{
    internal sealed class SagasDbContext : DbContext
    {
        public DbSet<SagaInstance> Sagas => Set<SagaInstance>();
        public DbSet<SagaStep> Steps => Set<SagaStep>();

        public SagasDbContext(DbContextOptions<SagasDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema(SagasConstants.SchemaName);
            builder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Sagas.Configurations"));
            builder.UseUtcDateTimes();
        }
    }
}