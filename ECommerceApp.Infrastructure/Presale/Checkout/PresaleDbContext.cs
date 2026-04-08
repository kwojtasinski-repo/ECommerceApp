using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Presale.Checkout
{
    internal sealed class PresaleDbContext : DbContext, IPresaleDbContext
    {
        public DbSet<CartLine> CartLines => Set<CartLine>();
        public DbSet<SoftReservation> SoftReservations => Set<SoftReservation>();
        public DbSet<StockSnapshot> StockSnapshots => Set<StockSnapshot>();

        public PresaleDbContext(DbContextOptions<PresaleDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema(PresaleConstants.SchemaName);
            modelBuilder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.Presale.Checkout.Configurations"));
            modelBuilder.UseUtcDateTimes();
        }
    }
}
