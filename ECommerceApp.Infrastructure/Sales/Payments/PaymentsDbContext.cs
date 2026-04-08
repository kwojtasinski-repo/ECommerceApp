using ECommerceApp.Domain.Sales.Payments;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Sales.Payments
{
    internal sealed class PaymentsDbContext : DbContext, IPaymentsDbContext
    {
        public DbSet<Payment> Payments => Set<Payment>();

        public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema(PaymentsConstants.SchemaName);
            modelBuilder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Sales.Payments.Configurations"));
            modelBuilder.UseUtcDateTimes();
        }
    }
}
