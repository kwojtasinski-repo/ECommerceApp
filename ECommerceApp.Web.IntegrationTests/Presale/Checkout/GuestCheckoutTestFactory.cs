using ECommerceApp.Application.AccountProfile.DTOs;
using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Identity.IAM;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Domain.Sales.Fulfillment;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Payments;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Web.IntegrationTests.Presale.Checkout
{
    /// <summary>
    /// Shared <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> for the
    /// ADR-0030 guest-checkout HTTP integration tests (Phases 1-3):
    /// <see cref="GuestCartIntegrationTests"/>, <see cref="GuestCheckoutIntegrationTests"/>,
    /// <see cref="GuestPromotionIntegrationTests"/>.
    ///
    /// Extends <see cref="BcWebApplicationFactory"/> (not the plain <c>CustomWebApplicationFactory</c>
    /// that <c>TusUploadTestFactory</c>/<c>WebValidationTestFactory</c> use) so every per-BC DbContext —
    /// Catalog, Presale, Sales, AccountProfile, and critically Messaging (the Outbox table the full
    /// guest checkout flow writes to via <c>OrderService.PlaceOrderFromPresaleAsync</c>) — runs against
    /// its own fresh EF InMemory database instead of this developer machine's real
    /// <c>Server=.;Database=ECommerceApp</c> SQL instance. That real database does not have every
    /// migration applied locally (confirmed: seeding a product through the real DB threw
    /// <c>SqlException: Invalid object name 'messaging.Outbox'</c> when these tests were first run
    /// against the plain <c>CustomWebApplicationFactory</c>) — a full order-placement flow is exactly
    /// the kind of test that must not depend on a specific developer machine's migration state.
    /// <see cref="BcWebApplicationFactory"/> also swaps <c>IMessageBroker</c> for a synchronous
    /// multi-handler broker, so <c>OrderPlacedHandler</c> (which removes committed soft reservations
    /// after an order is placed) runs deterministically before each HTTP response returns — required
    /// by <see cref="GuestCheckoutTestBase.PlaceGuestOrderAsync"/> being called twice in a row against
    /// the same guest cookie (the resubmission/idempotency test).
    ///
    /// <see cref="ICategoryService"/> is stubbed so <c>_Layout.cshtml</c>'s unconditional
    /// <c>GetAllCategories()</c> call never touches the Catalog schema — same precedent as
    /// <c>WebValidationTestFactory</c>/<c>TusUploadTestFactory</c>.
    /// </summary>
    public sealed class GuestCheckoutTestFactory : BcWebApplicationFactory
    {
        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICategoryService));
            if (descriptor != null) services.Remove(descriptor);
            services.AddScoped<ICategoryService, GuestCheckoutNullCategoryService>();
        }

        /// <summary>
        /// Seeds a real, published product with an available <see cref="StockSnapshot"/> so
        /// <c>SoftReservationService.HoldAsync</c> (invoked by <c>PlaceOrder</c> GET) can reserve it.
        /// </summary>
        public async Task<int> CreateAvailableProductAsync(decimal price = 50m, int stockQty = 100)
        {
            using var scope = Services.CreateScope();
            var sp = scope.ServiceProvider;

            var categoryRepo = sp.GetRequiredService<ICategoryRepository>();
            var categoryId = await categoryRepo.AddAsync(Category.Create($"Guest checkout category {Guid.NewGuid():N}"));

            var productService = sp.GetRequiredService<IProductService>();
            var productId = await productService.AddProduct(new CreateProductDto(
                $"Guest checkout product {Guid.NewGuid():N}", price, "guest checkout test product", categoryId.Value, new List<int>()));
            await productService.PublishProduct(productId);

            var snapshotRepo = sp.GetRequiredService<IStockSnapshotRepository>();
            await snapshotRepo.AddAsync(StockSnapshot.Create(productId, stockQty, DateTime.UtcNow));

            return productId;
        }

        /// <summary>Looks up a <see cref="UserProfile"/> by its (guest-token-or-Identity-id) UserId.</summary>
        public async Task<UserProfile> FindProfileByUserIdAsync(string userId)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
            return await repo.GetByUserIdAsync(userId);
        }

        public async Task<UserProfile> FindProfileByIdAsync(int profileId)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
            return await repo.GetByIdAsync(new UserProfileId(profileId));
        }

        public async Task<int> SeedUnclaimedProfileAsync(string email, string guestUserId)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
            return (await repo.AddAsync(UserProfile.Create(
                guestUserId,
                "Jan",
                "Kowalski",
                false,
                null,
                null,
                email,
                "500600700"))).Value;
        }

        public async Task<int?> GetOrderCustomerIdAsync(int orderId)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            return await repo.GetCustomerIdAsync(orderId);
        }

        /// <summary>The <c>gst_...</c> (or Identity user id) an order is placed under — what
        /// <c>MyPayments</c>/<c>MyRefunds</c> filter by. Used to seed a payment/refund row under the
        /// exact same identity a guest's own order was placed with.</summary>
        public async Task<string> GetOrderUserIdAsync(int orderId)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var order = await repo.GetByIdAsync(orderId);
            return order?.UserId.Value;
        }

        /// <summary>The id of the (single, in these tests) line item on a placed order — what
        /// <c>OrderItemsController.Details</c> is addressed by.</summary>
        public async Task<int> GetOrderItemIdAsync(int orderId)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var order = await repo.GetByIdWithItemsAsync(orderId);
            return order.OrderItems[0].Id.Value;
        }

        /// <summary>True if an <see cref="ApplicationUser"/> row exists with the given email —
        /// used to assert that guest checkout/failed promotion attempts never create one.</summary>
        public async Task<bool> ApplicationUserExistsAsync(string email)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<IUserManager<ApplicationUser>>();
            return userManager.Users.Any(u => u.Email == email);
        }

        public async Task<string> CreateRegisteredUserAsync(string email, string password)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            result.Succeeded.ShouldBeTrue(string.Join("; ", result.Errors));
            return user.Id;
        }

        /// <summary>
        /// Idempotently ensures a <see cref="UserProfile"/> row exists for the given (already-registered)
        /// Identity user id — used by the authenticated-flow regression test and the AlreadyRegistered
        /// promotion test, which both need a profile whose UserId already matches a real ApplicationUser.
        /// </summary>
        public async Task<int> EnsureProfileForRegisteredUserAsync(string userId, string email)
        {
            using var scope = Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IUserProfileService>();
            return await service.GetOrCreateForGuestAsync(
                userId, "Admin", "Tester", false, null, null, email, "500600700");
        }

        /// <summary>
        /// This factory doesn't wire up automatic Payment creation on order placement (see the class
        /// remarks), so tests that need to POST a real payment confirmation must seed one directly.
        /// </summary>
        public async Task<int> CreatePendingPaymentAsync(int orderId, string userId, decimal totalAmount = 50m, int currencyId = 1)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
            var payment = Payment.Create(
                new PaymentOrderId(orderId), totalAmount, currencyId, DateTime.UtcNow.AddMinutes(30), userId);
            await repo.AddAsync(payment);
            var saved = await repo.GetByOrderIdAsync(orderId);
            return saved.Id.Value;
        }

        /// <summary>Seeds a real <c>Refund</c> row directly (this factory doesn't drive the actual
        /// Refund.Request HTTP flow for seeding), used by tests that only need one to already exist,
        /// scoped to <paramref name="orderId"/>/<paramref name="userId"/>.</summary>
        public async Task<int> CreateRefundRequestAsync(int orderId, int productId, string userId, string reason = "Niezgodny towar")
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRefundRepository>();
            var refund = Refund.Create(orderId, reason, false, new[] { RefundItem.Create(productId, 1) }, userId);
            var id = await repo.AddAsync(refund);
            return id;
        }
    }

    /// <summary>No-op <see cref="ICategoryService"/> — only used to satisfy <c>_Layout.cshtml</c>'s
    /// navigation-menu call during these tests; never asserted against.</summary>
    internal sealed class GuestCheckoutNullCategoryService : ICategoryService
    {
        public Task<int> AddCategory(CreateCategoryDto dto) => Task.FromResult(0);
        public Task<bool> UpdateCategory(UpdateCategoryDto dto) => Task.FromResult(false);
        public Task<bool> DeleteCategory(int id) => Task.FromResult(false);
        public Task<CategoryVm> GetCategory(int id) => Task.FromResult<CategoryVm>(null);
        public Task<List<CategoryVm>> GetAllCategories() => Task.FromResult(new List<CategoryVm>());
    }
}
