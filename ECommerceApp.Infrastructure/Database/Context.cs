using ECommerceApp.Domain.Model;
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

            // -------------------- RELATION N:N --------------------
            builder.Entity<ItemTag>()
                .HasKey(it => new { it.ItemId, it.TagId });

            builder.Entity<ItemTag>()
                .HasOne(it => it.Item)
                .WithMany(i => i.ItemTags)
                .HasForeignKey(it => it.ItemId);

            builder.Entity<ItemTag>()
                .HasOne(it => it.Tag)
                .WithMany(t => t.ItemTags)
                .HasForeignKey(it => it.TagId);
            // -------------------- RELATION N:N --------------------

            // -------------------- RELATION 1:1 --------------------
            builder.Entity<Coupon>()
                .HasOne(c => c.CouponUsed)
                .WithOne(c => c.Coupon)
                .HasForeignKey<CouponUsed>(c => c.CouponId);
            // -------------------- RELATION 1:1 --------------------

            // -------------------- RELATION 1:1 --------------------
            builder.Entity<Order>()
                .HasOne(o => o.CouponUsed)
                .WithOne(c => c.Order)
                .HasForeignKey<CouponUsed>(c => c.OrderId);
            // -------------------- RELATION 1:1 --------------------

            // -------------------- RELATION 1:1 --------------------
            builder.Entity<Refund>()
                .HasOne(r => r.Order)
                .WithOne(o => o.Refund)
                .HasForeignKey<Order>(o => o.RefundId);
            // -------------------- RELATION 1:1 --------------------

            // -------------------- DELETE BEHAVIOUR --------------------
            builder.Entity<Item>()
                .HasMany(i => i.Images)
                .WithOne(it => it.Item)
                .OnDelete(DeleteBehavior.SetNull);
            // -------------------- DELETE BEHAVIOUR --------------------

            // -------------------- USER SEED DATA ----------------------
            //Seeding a  'Administrator' role to AspNetRoles table
            builder.Entity<IdentityRole>().HasData(new IdentityRole { Id = "Administrator", Name = "Administrator", NormalizedName = "ADMINISTRATOR".ToUpper() });
            builder.Entity<IdentityRole>().HasData(new IdentityRole { Id = "Manager", Name = "Manager", NormalizedName = "MANAGER".ToUpper() });
            builder.Entity<IdentityRole>().HasData(new IdentityRole { Id = "Service", Name = "Service", NormalizedName = "SERVICE".ToUpper() });
            builder.Entity<IdentityRole>().HasData(new IdentityRole { Id = "User", Name = "User", NormalizedName = "USER".ToUpper() });
            builder.Entity<IdentityRole>().HasData(new IdentityRole { Id = "NotRegister", Name = "NotRegister", NormalizedName = "NOTREGISTER".ToUpper() });


            //a hasher to hash the password before seeding the user to the db
            var hasher = new PasswordHasher<ApplicationUser>();


            //Seeding the User to AspNetUsers table
            builder.Entity<ApplicationUser>().HasData(
                new ApplicationUser
                {
                    Id = "8e445865-a24d-4543-a6c6-9443d048cdb9", // primary key
                    UserName = "admin@localhost",
                    Email = "admin@localhost",
                    NormalizedUserName = "ADMIN@LOCALHOST",
                    //PasswordHash = hasher.HashPassword(null, "aDminN@W25!"),
                    PasswordHash = "AQAAAAEAACcQAAAAELdaCtFvYS8X6XMmd9kWXKoe5TE3YEGIhePJXcIqiY6p6MdTT0XjQLI9OrLC6yOVvw==", // password aDminN@W25!
                    EmailConfirmed = true,
                    SecurityStamp = string.Empty
                }
            );

            //Seeding the relation between our user and role to AspNetUserRoles table
            builder.Entity<IdentityUserRole<string>>().HasData(
                new IdentityUserRole<string>
                {
                    RoleId = "Administrator",
                    UserId = "8e445865-a24d-4543-a6c6-9443d048cdb9"
                }
            );
            // -------------------- USER SEED DATA ----------------------

            // -------------------- CONTACT DETAIL TYPE SEED DATA ----------------------
            builder.Entity<ContactDetailType>().HasData(new ContactDetailType { Id = 1, Name = "PhoneNumber" });
            builder.Entity<ContactDetailType>().HasData(new ContactDetailType { Id = 2, Name = "Email" });
            // -------------------- CONTACT DETAIL TYPE SEED DATA ----------------------

            // -------------------- CURRENCY SEED DATA ----------------------
            builder.Entity<Currency>().HasData(new Currency { Id = 1, Code = "PLN", Description = "Polski złoty" });
            // -------------------- CURRENCY SEED DATA ----------------------

            // -------------------- SET PROPERTY --------------------
            builder.Entity<Currency>().Property(c => c.Code).IsRequired().HasMaxLength(3);
            builder.Entity<CurrencyRate>().Property(cr => cr.Rate).HasColumnType("decimal(18,4)").IsRequired();
            // -------------------- SET PEROPERTY --------------------

            // -------------------- ADD INDEX --------------------
            builder.Entity<Currency>().HasIndex(c => new { c.Code }).IsUnique(true);
            // -------------------- ADD INDEX --------------------

            var keysProperties = builder.Model.GetEntityTypes().Select(x => x.FindPrimaryKey()).SelectMany(x => x.Properties);
            foreach (var property in keysProperties)
            {
                property.ValueGenerated = ValueGenerated.OnAdd;
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.LogTo(System.Console.WriteLine);
        }
    }
}
