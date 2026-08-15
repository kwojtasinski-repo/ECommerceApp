using ECommerceApp.Application.AccountProfile.Options;
using ECommerceApp.Domain.AccountProfile;
using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Infrastructure.AccountProfile;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.AccountProfile
{
    public class GuestProfileCleanupScheduledJobReconcilerIntegrationTests : BcBaseTest<IUserProfileRepository>
    {
        public GuestProfileCleanupScheduledJobReconcilerIntegrationTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task ReconcileAsync_SecondRunWithChangedConfig_UpdatesScheduleColumnInDatabase()
        {
            var startupReconciler = GetRequiredService<System.Collections.Generic.IEnumerable<IHostedService>>()
                .OfType<GuestProfileCleanupScheduledJobReconciler>()
                .Single();
            var jobs = GetRequiredService<IScheduledJobRepository>();

            var first = await jobs.GetByNameAsync("UnclaimedGuestProfileCleanup", CancellationToken);
            first.ShouldNotBeNull();
            first.Schedule.Value.ShouldBe("0 4 * * *");

            // GuestProfileCleanupOptions is bound via IOptions<T> (unlike MessagingOptions' mutable
            // singleton), so simulate a config change by reconciling with a fresh IOptions instance.
            var changedReconciler = new GuestProfileCleanupScheduledJobReconciler(
                GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new GuestProfileCleanupOptions { Schedule = "15 6 * * *" }),
                GetRequiredService<Microsoft.Extensions.Logging.ILogger<GuestProfileCleanupScheduledJobReconciler>>());

            await changedReconciler.ReconcileAsync(CancellationToken);

            // Re-resolve: BcWebApplicationFactory makes all BC-scoped services (incl. this
            // repository) Transient, so reusing `jobs` here would return its own DbContext's
            // stale tracked entity rather than re-querying the (shared InMemory) store.
            var freshJobs = GetRequiredService<IScheduledJobRepository>();
            var second = await freshJobs.GetByNameAsync("UnclaimedGuestProfileCleanup", CancellationToken);
            second.ShouldNotBeNull();
            second.Schedule.Value.ShouldBe("15 6 * * *");
            startupReconciler.ShouldNotBeNull();
        }
    }
}
