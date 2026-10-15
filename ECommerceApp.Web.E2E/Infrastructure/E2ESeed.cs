using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.Application.Supporting.Currencies.Services;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Identity.IAM;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.E2E.Infrastructure
{
    /// <summary>
    /// Seeds the data a browser test needs against the real SQL Server database shared by every
    /// <see cref="PlaywrightWebApplicationFactory"/> instance in the run (Phase 22's isolation model:
    /// one database, unique identifiers per test). Each factory still gets its own private InMemory IAM
    /// store (see <see cref="PlaywrightWebApplicationFactory.OverrideServicesImplementation"/>), so the
    /// fixed emails below are safe to reuse across factories via the idempotent look-up-or-create checks
    /// in <see cref="AdminAsync"/>/<see cref="CustomerAsync"/>. Product and category names are not
    /// idempotent — every call creates new rows — so they carry a per-call GUID suffix instead.
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

        /// <summary>
        /// Creates a disposable user (unique email per call) that a test may delete through the UI.
        /// Returns the user name shown in the user list.
        /// </summary>
        public static async Task<string> DisposableUserAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var email = $"e2e-delete-{Guid.NewGuid():N}@example.com";
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, Password);
            result.Succeeded.ShouldBeTrue(string.Join("; ", result.Errors));
            return email;
        }

        /// <summary>
        /// Creates a currency a test may delete through the UI. Currency codes must be valid ISO 4217
        /// codes, so a per-call GUID suffix is not possible; the caller passes a code reserved for its test.
        /// </summary>
        public static async Task CurrencyAsync(IServiceProvider services, string code)
        {
            using var scope = services.CreateScope();
            var currencyService = scope.ServiceProvider.GetRequiredService<ICurrencyService>();
            await currencyService.AddAsync(new CreateCurrencyDto(code, $"E2E currency {code}"));
        }

        public static async Task<IReadOnlyList<int>> LifecycleProductsAsync(IServiceProvider services) =>
            (await LifecycleProductsWithNamesAsync(services)).Select(p => p.Id).ToList();

        public static Task<IReadOnlyList<(int Id, string Name)>> LifecycleProductsWithNamesAsync(IServiceProvider services) =>
            ProductsAsync(services, "E2E Lifecycle Category", "E2E Lifecycle Product A", "E2E Lifecycle Product B");

        public static async Task<IReadOnlyList<int>> BasketProductsAsync(IServiceProvider services) =>
            (await BasketProductsWithNamesAsync(services)).Select(p => p.Id).ToList();

        public static Task<IReadOnlyList<(int Id, string Name)>> BasketProductsWithNamesAsync(IServiceProvider services) =>
            ProductsAsync(services, "E2E Basket Category", "E2E Basket Product A", "E2E Basket Product B");

        /// <summary>
        /// Product names carry a per-call GUID suffix, same as the category name above: on the shared
        /// database every test class that seeds "basket"/"lifecycle" products contributes rows with the
        /// same base name, and the storefront looks products up by name-text match. A fixed name across
        /// many calls resolves to several matching cards and Playwright throws a strict-mode violation
        /// (multiple elements matched) instead of clicking the one this test just created.
        /// </summary>
        private static async Task<IReadOnlyList<(int Id, string Name)>> ProductsAsync(
            IServiceProvider services,
            string categoryName,
            string firstProductName,
            string secondProductName)
        {
            using var scope = services.CreateScope();
            var categoryRepository = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var categoryId = await categoryRepository.AddAsync(Category.Create($"{categoryName} {Guid.NewGuid():N}"));
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
            var stockRepository = scope.ServiceProvider.GetRequiredService<IStockSnapshotRepository>();

            var suffix = Guid.NewGuid().ToString("N");
            var products = new List<(int Id, string Name)>();
            foreach (var product in new[]
            {
                (Name: $"{firstProductName} {suffix}", Price: 19.99m),
                (Name: $"{secondProductName} {suffix}", Price: 29.99m)
            })
            {
                var productId = await productService.AddProduct(new CreateProductDto(
                    product.Name, product.Price, "E2E product", categoryId.Value, new List<int>()));
                await productService.PublishProduct(productId);
                await stockRepository.AddAsync(StockSnapshot.Create(productId, 100, DateTime.UtcNow));
                products.Add((productId, product.Name));
            }

            return products;
        }
    }
}
