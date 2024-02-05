using ECommerceApp.Application.Constants;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Database.SeedData
{
    internal static class Seed
    {
        public static ModelBuilder ApplySeed(this ModelBuilder builder)
        {
            // -------------------- USER SEED DATA ----------------------
            //Seeding a  'Administrator' role to AspNetRoles table
            builder.Entity<IdentityRole>().HasData(new IdentityRole { Id = "Administrator", Name = "Administrator", NormalizedName = "ADMINISTRATOR" });
            builder.Entity<IdentityRole>().HasData(new IdentityRole { Id = "Manager", Name = "Manager", NormalizedName = "MANAGER" });
            builder.Entity<IdentityRole>().HasData(new IdentityRole { Id = "Service", Name = "Service", NormalizedName = "SERVICE" });
            builder.Entity<IdentityRole>().HasData(new IdentityRole { Id = "User", Name = "User", NormalizedName = "USER" });
            builder.Entity<IdentityRole>().HasData(new IdentityRole { Id = "NotRegister", Name = "NotRegister", NormalizedName = "NOTREGISTER" });

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
            builder.Entity<Currency>().HasData(new Currency { Id = CurrencyConstants.PlnId, Code = "PLN", Description = "Polski złoty" });
            // -------------------- CURRENCY SEED DATA ----------------------

            return builder;
        }
    }
}
