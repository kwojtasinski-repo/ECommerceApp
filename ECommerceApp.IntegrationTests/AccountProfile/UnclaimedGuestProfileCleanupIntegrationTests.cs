using ECommerceApp.Application.AccountProfile.Services;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Shared.TestInfrastructure.TestData;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.AccountProfile
{
    /// <summary>
    /// Cross-BC integration coverage for <see cref="UnclaimedGuestProfileCleanupTask"/> (ADR-0030
    /// Phase 4): AccountProfile (candidate profiles), Identity (claim-status lookup), and Orders
    /// (the "never delete a profile with an order" guard) all participate for real, no mocks.
    /// </summary>
    public class UnclaimedGuestProfileCleanupIntegrationTests : BcBaseTest<IUserProfileRepository>
    {
        public UnclaimedGuestProfileCleanupIntegrationTests(ITestOutputHelper output) : base(output) { }

        private static OrderCustomer CreateOrderCustomer() => new(
            "Anna", "Nowak", "anna@test.com", "987654321",
            false, null, null, "Lipowa", "5", null, "00-001", "Warszawa", "Polska");

        private async Task<UserProfile> SeedProfileAsync(string userId, DateTime createdAt)
        {
            var profile = UserProfileTestData.Create(userId);
            SetPrivateProperty(profile, nameof(UserProfile.CreatedAt), createdAt);
            await GetRequiredService<IUserProfileRepository>().AddAsync(profile);
            return profile;
        }

        private async Task SeedOrderForCustomerAsync(int customerId)
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(customerId, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateOrderCustomer());
            await repo.AddAsync(order, CancellationToken);
        }

        private async Task<string> SeedRegisteredUserIdAsync(string email)
        {
            var result = await GetRequiredService<IGuestAccountProvisioner>().CreateAsync(email, "P@ssw0rd123!");
            result.Succeeded.ShouldBeTrue();
            return result.UserId;
        }

        private IScheduledTask CleanupTask
            => GetRequiredService<IEnumerable<IScheduledTask>>()
                .Single(task => task.TaskName == "UnclaimedGuestProfileCleanup");

        private static void SetPrivateProperty<T>(T instance, string propertyName, object value)
            => typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
                .SetValue(instance, value);

        [Fact]
        public async Task ExecuteAsync_MixedDataset_DeletesOnlyUnclaimedOldOrderlessProfiles()
        {
            var old = DateTime.UtcNow.AddDays(-120);
            var recent = DateTime.UtcNow.AddDays(-5);

            // 1. Unclaimed, old, no order → must be deleted.
            var deletable = await SeedProfileAsync("gst_deletable", old);

            // 2. Unclaimed, old, HAS an order → must survive (critical invariant).
            var withOrder = await SeedProfileAsync("gst_with_order", old);
            await SeedOrderForCustomerAsync(withOrder.Id.Value);

            // 3. Claimed (real ApplicationUser), old → must survive regardless of age.
            var claimedUserId = await SeedRegisteredUserIdAsync("claimed-cleanup-test@example.com");
            var claimed = await SeedProfileAsync(claimedUserId, old);

            // 4. Unclaimed, too recent → must survive.
            var tooRecent = await SeedProfileAsync("gst_too_recent", recent);

            var context = new JobExecutionContext(null, "cleanup-1");
            await CleanupTask.ExecuteAsync(context, CancellationToken);

            context.Outcome.ShouldBeOfType<ECommerceApp.Application.Supporting.TimeManagement.Models.JobOutcome.Success>();

            var repo = GetRequiredService<IUserProfileRepository>();
            (await repo.GetByIdAsync(deletable.Id)).ShouldBeNull("unclaimed + old + no order must be deleted");
            (await repo.GetByIdAsync(withOrder.Id)).ShouldNotBeNull("a profile with an order must never be deleted");
            (await repo.GetByIdAsync(claimed.Id)).ShouldNotBeNull("a claimed profile must never be deleted");
            (await repo.GetByIdAsync(tooRecent.Id)).ShouldNotBeNull("a too-recent profile must never be deleted");
        }

        [Fact]
        public async Task ExecuteAsync_NoCandidates_ReportsSuccessAndDeletesNothing()
        {
            var context = new JobExecutionContext(null, "cleanup-2");

            await CleanupTask.ExecuteAsync(context, CancellationToken);

            context.Outcome.ShouldBeOfType<ECommerceApp.Application.Supporting.TimeManagement.Models.JobOutcome.Success>();
        }
    }
}
