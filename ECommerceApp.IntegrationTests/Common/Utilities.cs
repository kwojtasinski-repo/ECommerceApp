using ECommerceApp.Infrastructure;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.IntegrationTests.Common
{
    internal class Utilities
    {
        public static void InitilizeDbForTests(Context context)
        {
            // ---------------------------------- Dane testowe ----------------------------------

            var customer = new Domain.Model.Customer { Id = 1, FirstName = "Mr", LastName = "Tester", IsCompany = false, UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e" };
            context.Add(customer);

            var address = new Domain.Model.Address { Id = 1, BuildingNumber = "2", FlatNumber = 10, City = "Nowa Sól", Country = "Poland", Street = "Testowa" , CustomerId = 1, ZipCode = 67100 };
            context.Add(address);

            var contactDetail = new Domain.Model.ContactDetail() { Id = 1, ContactDetailInformation = "867123563", ContactDetailTypeId = 1, CustomerId = 1 };
            context.Add(contactDetail);

            var brand = new Domain.Model.Brand() { Id = 1, Name = "Samsung" };
            context.Add(brand);
            var brand2 = new Domain.Model.Brand() { Id = 2, Name = "Goplana" };
            context.Add(brand2);
            var brand3 = new Domain.Model.Brand() { Id = 3, Name = "Xiaomi" };
            context.Add(brand3);

            var type = new Domain.Model.Type { Id = 100, Name = "TypeDelete" };
            context.Add(type);

            var contactDetailType = new Domain.Model.ContactDetailType { Id = 3, Name = "ContactDetailTypeDelete" };
            context.Add(contactDetailType);

            var currency1 = new Domain.Model.Currency { Id = 2, Code = "EUR", Description = "Euro" };
            context.Add(currency1);
            var currency2 = new Domain.Model.Currency { Id = 3, Code = "CHF", Description = "Frank szwajcarski" };
            context.Add(currency2);
            var currency3 = new Domain.Model.Currency { Id = 4, Code = "USD", Description = "Dolar amerykański" };
            context.Add(currency3);
            var currency4 = new Domain.Model.Currency { Id = 5, Code = "ABC", Description = "Test Test" };
            context.Add(currency4);

            var currencyRate1 = new Domain.Model.CurrencyRate { Id = 1, Currency = currency1, CurrencyDate = DateTime.Now.Date, CurrencyId = 1, Rate = 1 };
            context.Add(currencyRate1);
            var currencyRate2 = new Domain.Model.CurrencyRate { Id = 2, Currency = currency2, CurrencyDate = DateTime.Now.Date, CurrencyId = 2, Rate = new decimal(0.5214) };
            context.Add(currencyRate2);

            //user: { email: "test@test", userName: "test@test", password: "Test@test12" }
            var testUser = new IdentityUser 
            { 
                Id = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e", 
                Email = "test@test", 
                UserName = "test@test",
                NormalizedUserName = "TEST@TEST",
                PasswordHash = "AQAAAAEAACcQAAAAEAhNvr909GdhKMLVvTQ6kj17HAWZPg6c+YgQ8rl/m1Ww6Pf+fqJ8FUf+yU5N5stXOA==", ConcurrencyStamp = "db68806b-190f-4abf-a39f-9b2d74039dd9",
                SecurityStamp = string.Empty,
                EmailConfirmed = true
            };
            context.Add(testUser);
            
            var userRole = new IdentityUserRole<string>
            {
                RoleId = "Administrator",
                UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e"
            };
            context.Add(userRole);


            // ---------------------------------- Dane testowe ----------------------------------

            context.SaveChanges();
        }
    }
}
