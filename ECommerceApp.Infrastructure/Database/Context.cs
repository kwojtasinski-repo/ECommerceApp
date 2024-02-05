using ECommerceApp.Application.Constants;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database.SeedData;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq;

namespace ECommerceApp.Infrastructure.Database
{
    public class Context : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public DbSet<Address> Addresses { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<ContactDetail> ContactDetails { get; set; }
        public DbSet<ContactDetailType> ContactDetailTypes { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponType> CouponTypes { get; set; }
        public DbSet<CouponUsed> CouponUsed { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<ItemTag> ItemTag { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItem { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Refund> Refunds { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Type> Types { get; set; }
        public DbSet<Image> Images { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<CurrencyRate> CurrencyRates { get; set; }

        public Context(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(GetType().Assembly);
            builder.ApplySeed();

            var keysProperties = builder.Model.GetEntityTypes().Select(x => x.FindPrimaryKey()).SelectMany(x => x.Properties);
            foreach (var property in keysProperties)
            {
                property.ValueGenerated = ValueGenerated.OnAdd;
            }
        }
    }
}
