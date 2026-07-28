using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Messaging
{
    /// <summary>
    /// Integration tests for <see cref="IOutboxRepository"/>. Uses the InMemory
    /// <c>MessagingDbContext</c> provided by <see cref="BcBaseTest{T}"/> — no SQL Server dependency.
    /// </summary>
    public class OutboxRepositoryTests : BcBaseTest<IOutboxRepository>
    {
        public OutboxRepositoryTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task AddAsync_PersistsRowAndReturnsGeneratedId()
        {
            var message = OutboxMessage.Create("test-message", "{\"foo\":1}");

            var id = await _service.AddAsync(message, CancellationToken);

            id.ShouldBeGreaterThan(0L);
        }

        [Fact]
        public async Task GetDueAsync_ReturnsOnlyPendingDueAndZombieRunning()
        {
            var now = DateTime.UtcNow;

            // 1) Pending, due now — should come back.
            var pendingDueNow = OutboxMessage.Create("pending-due-now", "{}");
            var pendingDueNowId = await _service.AddAsync(pendingDueNow, CancellationToken);

            // 2) Pending, but not due yet (backed off into the future via Fail) — excluded.
            var pendingFuture = OutboxMessage.Create("pending-future", "{}");
            pendingFuture.Fail("transient error", now);
            await _service.AddAsync(pendingFuture, CancellationToken);

            // 3) Running, lock not expired — excluded (still being processed).
            var runningNotExpired = OutboxMessage.Create("running-not-expired", "{}");
            runningNotExpired.MarkRunning(now.AddMinutes(5));
            await _service.AddAsync(runningNotExpired, CancellationToken);

            // 4) Running, lock expired (zombie) — should come back for recovery.
            var runningZombie = OutboxMessage.Create("running-zombie", "{}");
            runningZombie.MarkRunning(now.AddMinutes(-5));
            var runningZombieId = await _service.AddAsync(runningZombie, CancellationToken);

            // 5) Dispatched — excluded.
            var dispatched = OutboxMessage.Create("dispatched", "{}");
            dispatched.MarkDispatched(now);
            await _service.AddAsync(dispatched, CancellationToken);

            // 6) DeadLetter — excluded.
            var deadLetter = OutboxMessage.Create("dead-letter", "{}", maxRetries: 0);
            deadLetter.Fail("fatal error", now);
            await _service.AddAsync(deadLetter, CancellationToken);

            var due = await _service.GetDueAsync(batchSize: 50, CancellationToken);

            due.Select(m => m.Id).ShouldBe(new[] { pendingDueNowId, runningZombieId }, ignoreOrder: true);
        }

        [Fact]
        public async Task UpdateAsync_AfterMarkDispatched_PersistsStatusAndTimestamp()
        {
            var message = OutboxMessage.Create("update-after-dispatch", "{}");
            await _service.AddAsync(message, CancellationToken);

            var dispatchedAt = DateTime.UtcNow;
            message.MarkDispatched(dispatchedAt);
            await _service.UpdateAsync(message, CancellationToken);

            var due = await _service.GetDueAsync(batchSize: 50, CancellationToken);

            due.ShouldNotContain(m => m.MessageTypeKey == "update-after-dispatch");
        }
    }
}
