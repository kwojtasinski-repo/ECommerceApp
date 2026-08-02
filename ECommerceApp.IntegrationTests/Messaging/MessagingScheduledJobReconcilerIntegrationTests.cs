using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Messaging
{
    public class MessagingScheduledJobReconcilerIntegrationTests : BcBaseTest<IOutboxRepository>
    {
        public MessagingScheduledJobReconcilerIntegrationTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task ReconcileAsync_SecondRunWithChangedConfig_UpdatesScheduleColumnInDatabase()
        {
            var options = GetRequiredService<MessagingOptions>();
            var reconciler = GetRequiredService<System.Collections.Generic.IEnumerable<IHostedService>>()
                .OfType<MessagingScheduledJobReconciler>()
                .Single();
            var jobs = GetRequiredService<IScheduledJobRepository>();

            var first = await jobs.GetByNameAsync("OutboxCleanup", CancellationToken);
            first.ShouldNotBeNull();
            first.Schedule.Value.ShouldBe("0 3 * * *");

            options.OutboxCleanupSchedule = "15 6 * * *";
            await reconciler.ReconcileAsync(CancellationToken);

            var freshJobs = GetRequiredService<IScheduledJobRepository>();
            var second = await freshJobs.GetByNameAsync("OutboxCleanup", CancellationToken);
            second.ShouldNotBeNull();
            second.Schedule.Value.ShouldBe("15 6 * * *");
        }
    }
}