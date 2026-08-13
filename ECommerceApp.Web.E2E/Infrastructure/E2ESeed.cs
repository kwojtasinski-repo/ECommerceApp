using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Identity.IAM;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.Infrastructure
{
    /// <summary>
    /// Seeds the data a browser test needs into one <see cref="PlaywrightWebApplicationFactory"/>'s
    /// own host. Every factory owns a private IAM database and private InMemory bounded-context
    /// databases, so the fixed emails and product names below never collide between tests, including
    /// when test classes run in parallel.
    /// </summary>
    internal static class E2ESeed
    {
        public const string CustomerEmail = "e2e-order@example.com";
        public const string AdminEmail = "e2e-admin@example.com";
        public const string Password = "E2e@test12";

        public static async Task CustomerAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(CustomerEmail);
            if (user is not null)
            {
                return;
            }

            user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = CustomerEmail,
                Email = CustomerEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, Password);
            result.Succeeded.ShouldBeTrue(string.Join("; ", result.Errors));

            var profileService = scope.ServiceProvider.GetRequiredService<IUserProfileService>();
            var profileId = await profileService.CreateAsync(new CreateUserProfileDto(
                user.Id,
                "Jan",
                "Kowalski",
                false,
                null,
                null,
                CustomerEmail,
                "+48123456789"));
            (await profileService.AddAddressAsync(profileId, user.Id, new AddAddressDto(
                profileId,
                "Testowa",
                "1",
                null,
                "00-001",
                "Warszawa",
                "PL"))).ShouldBeTrue();
        }

        public static async Task AdminAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync("Administrator"))
            {
                (await roleManager.CreateAsync(new IdentityRole("Administrator"))).Succeeded.ShouldBeTrue();
            }

            var user = await userManager.FindByEmailAsync(AdminEmail);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = AdminEmail,
                    Email = AdminEmail,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, Password);
                result.Succeeded.ShouldBeTrue(string.Join("; ", result.Errors));
            }

            if (!await userManager.IsInRoleAsync(user, "Administrator"))
            {
                (await userManager.AddToRoleAsync(user, "Administrator")).Succeeded.ShouldBeTrue();
            }
        }

        public static Task<IReadOnlyList<int>> LifecycleProductsAsync(IServiceProvider services) =>
            ProductsAsync(services, "E2E Lifecycle Category", "E2E Lifecycle Product A", "E2E Lifecycle Product B");

        public static Task<IReadOnlyList<int>> BasketProductsAsync(IServiceProvider services) =>
            ProductsAsync(services, "E2E Basket Category", "E2E Basket Product A", "E2E Basket Product B");

        private static async Task<IReadOnlyList<int>> ProductsAsync(
            IServiceProvider services,
            string categoryName,
            string firstProductName,
            string secondProductName)
        {
            using var scope = services.CreateScope();
            var categoryRepository = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var categoryId = await categoryRepository.AddAsync(Category.Create(categoryName));
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
            var stockRepository = scope.ServiceProvider.GetRequiredService<IStockSnapshotRepository>();

            var productIds = new List<int>();
            foreach (var product in new[]
            {
                (Name: firstProductName, Price: 19.99m),
                (Name: secondProductName, Price: 29.99m)
            })
            {
                var productId = await productService.AddProduct(new CreateProductDto(
                    product.Name, product.Price, "E2E product", categoryId.Value, new List<int>()));
                await productService.PublishProduct(productId);
                await stockRepository.AddAsync(StockSnapshot.Create(productId, 100, DateTime.UtcNow));
                productIds.Add(productId);
            }

            return productIds;
        }
    }
}
