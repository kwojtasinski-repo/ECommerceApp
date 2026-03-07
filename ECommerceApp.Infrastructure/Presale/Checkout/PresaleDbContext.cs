using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Presale.Checkout
{
    internal sealed class PresaleDbContext : DbContext
    {
        public DbSet<Cart> Carts => Set<Cart>();
        public DbSet<CartItem> CartItems => Set<CartItem>();

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
        }
    }
}
