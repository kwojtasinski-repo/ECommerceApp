using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Identity.IAM;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    [Collection(PlaywrightCollection.Name)]
    public sealed class PlaywrightFixtureSmokeTests : IClassFixture<PlaywrightWebApplicationFactory>
    {
        private readonly PlaywrightBrowserFixture _browserFixture;
        private readonly PlaywrightWebApplicationFactory _factory;
        private readonly ITestOutputHelper _output;

        public PlaywrightFixtureSmokeTests(
            PlaywrightBrowserFixture browserFixture,
            PlaywrightWebApplicationFactory factory,
            ITestOutputHelper output)
        {
            _browserFixture = browserFixture;
            _factory = factory;
            _output = output;
        }

        [Fact]
        public async Task LoginPage_LoadsThroughKestrelAndPlaywright()
        {
            _factory.StartKestrelHost();
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            ILoginPage loginPage = await LoginPage.NavigateAsync(page, _factory.ServerAddress);

            (await loginPage.IsDisplayed()).ShouldBeTrue();
        }

        [Fact]
        public async Task LoginPage_SubmitLogin_BlankCredentials_ShowsValidationError()
        {
            _factory.StartKestrelHost();
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            ILoginPage loginPage = await LoginPage.NavigateAsync(page, _factory.ServerAddress);
            loginPage = await loginPage.SubmitLogin(string.Empty, string.Empty);

            (await loginPage.HasValidationError()).ShouldBeTrue();
        }

        [Fact]
        public async Task Products_Cart_CustomerForm_CreatesOrderWithoutPayment()
        {
            _factory.Sink.SetOutput(_output);
            var services = _factory.StartKestrelHost();
            await SeedBrowserUserAsync(services);
            var productIds = await SeedProductsAsync(services);

            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            var loginPage = await LoginPage.NavigateAsync(page, _factory.ServerAddress);
            await loginPage.LoginAsync("e2e-order@example.com", "E2e@test12");

            var storefront = await StorefrontPage.NavigateAsync(page, _factory.ServerAddress);
            var firstProduct = await storefront.OpenProductAsync("E2E Basket Product A");
            await firstProduct.AddToCartAsync(productIds[0], 2);

            storefront = await StorefrontPage.NavigateAsync(page, _factory.ServerAddress);
            var secondProduct = await storefront.OpenProductAsync("E2E Basket Product B");
            await secondProduct.AddToCartAsync(productIds[1], 3);

            var cart = await CartPage.NavigateAsync(page, _factory.ServerAddress);
            cart = await cart.ShouldContainProductAsync("E2E Basket Product A", 2);
            await cart.ShouldContainProductAsync("E2E Basket Product B", 3);

            var orderForm = await cart.ProceedToOrderAsync();
            orderForm = await orderForm.FillCustomerAsync();
            var summary = await orderForm.SubmitAsync();

            await summary.ShouldConfirmOrderAsync();
        }

        private static async Task SeedBrowserUserAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var email = "e2e-order@example.com";
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null)
            {
                return;
            }

            user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, "E2e@test12");
            result.Succeeded.ShouldBeTrue(string.Join("; ", result.Errors));

            var profileService = scope.ServiceProvider.GetRequiredService<IUserProfileService>();
            var profileId = await profileService.CreateAsync(new CreateUserProfileDto(
                user.Id,
                "Jan",
                "Kowalski",
                false,
                null,
                null,
                email,
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

        private static async Task<IReadOnlyList<int>> SeedProductsAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var categoryRepository = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();
            var categoryId = await categoryRepository.AddAsync(Category.Create("E2E Basket Category"));
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
            var stockRepository = scope.ServiceProvider.GetRequiredService<IStockSnapshotRepository>();

            var productIds = new List<int>();
            foreach (var product in new[]
            {
                (Name: "E2E Basket Product A", Price: 19.99m),
                (Name: "E2E Basket Product B", Price: 29.99m)
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
