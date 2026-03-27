using ECommerceApp.Application.Constants;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Database.SeedData
{
    internal static class Seed
    {
        public static ModelBuilder ApplySeed(this ModelBuilder builder)
        {
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
