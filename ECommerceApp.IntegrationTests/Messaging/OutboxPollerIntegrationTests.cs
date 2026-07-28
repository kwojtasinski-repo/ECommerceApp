using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Messaging
{
    /// <summary>
    /// End-to-end tests for <see cref="OutboxPollerService"/> — the only test class in this suite
    /// that exercises the real, running <c>BackgroundService</c> poll loop (registered via
    /// <c>AddMessagingInfrastructure</c>, started automatically when the test host builds). Uses a
    /// throwaway test-only message/handler pair (registered via <see cref="OverrideServicesImplementation"/>)
    /// so these tests don't depend on any real BC's handler behavior.
    /// <para>
    /// The poller ticks every 10 seconds (see <see cref="OutboxPollerService"/>), so every assertion
    /// here uses a bounded retry-poll (check every ~500ms up to a generous timeout) instead of a single
    /// fixed <c>Task.Delay</c> + assert — a single wait would be inherently racy against the timer.
    /// </para>
    /// </summary>
    public class OutboxPollerIntegrationTests : BcBaseTest<IOutboxRepository>
    {
        // MessageTypeRegistry keys its internal dictionary by CLR Type, so PollerTestMessage can only
        // ever be registered once for the lifetime of the test process.
        private static readonly string TestMessageKey = $"outbox-poller-tests-message-{Guid.NewGuid():N}";

        static OutboxPollerIntegrationTests()
        {
            MessageTypeRegistry.Register(typeof(PollerTestMessage), TestMessageKey);
        }

        private static readonly ConcurrentBag<Guid> ReceivedCorrelationIds = new();

        public OutboxPollerIntegrationTests(ITestOutputHelper output) : base(output) { }

        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            base.OverrideServicesImplementation(services);
            services.AddSingleton(ReceivedCorrelationIds);
            services.AddScoped<IMessageHandler<PollerTestMessage>, PollerTestMessageHandler>();
        }

        private sealed class PollerTestMessage : IMessage
        {
            public Guid CorrelationId { get; init; }
        }

        private sealed class PollerTestMessageHandler : IMessageHandler<PollerTestMessage>
        {
            private readonly ConcurrentBag<Guid> _received;
            public PollerTestMessageHandler(ConcurrentBag<Guid> received) => _received = received;

            public Task HandleAsync(PollerTestMessage message, CancellationToken ct = default)
            {
                _received.Add(message.CorrelationId);
                return Task.CompletedTask;
            }
        }

        private async Task<long> InsertDueMessageAsync(Guid correlationId)
        {
            var payload = JsonSerializer.Serialize(new PollerTestMessage { CorrelationId = correlationId });
            var message = OutboxMessage.Create(TestMessageKey, payload);
            return await _service.AddAsync(message, CancellationToken);
        }

        private async Task<OutboxStatus> GetStatusAsync(long id)
        {
            var context = GetRequiredService<IMessagingDbContext>();
            var row = await context.Outbox.AsNoTracking().FirstAsync(m => m.Id == id, CancellationToken);
            return row.Status;
        }

        /// <summary>
        /// Bounded retry-poll: checks <paramref name="condition"/> every 500ms up to
        /// <paramref name="timeout"/>. Returns as soon as the condition is true; never throws on
        /// timeout — the caller asserts on the final observed state, so a timeout produces a clear
        /// assertion failure with the actual last-seen value, not an opaque wait-timeout exception.
        /// </summary>
        private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition())
                    return;

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        [Fact]
        public async Task PendingRow_IsPickedUpAndDispatched_WithinOnePollCycle()
        {
            var correlationId = Guid.NewGuid();
            var id = await InsertDueMessageAsync(correlationId);

            await WaitUntilAsync(
                async () => await GetStatusAsync(id) == OutboxStatus.Dispatched,
                TimeSpan.FromSeconds(20));

            (await GetStatusAsync(id)).ShouldBe(OutboxStatus.Dispatched);
            ReceivedCorrelationIds.ShouldContain(correlationId);
        }

        /// <summary>
        /// Crash/restart simulation — the single most important test in this phase. A row is marked
        /// <c>Running</c> with an already-expired lock, simulating "a previous poller process died
        /// mid-dispatch and never updated the row." This proves at-least-once redelivery actually
        /// happens after a simulated crash: the poller must detect the zombie, reset it to
        /// <c>Pending</c> on one tick, and dispatch it (invoking the handler for real) on a later tick
        /// — not just that the happy path works.
        /// </summary>
        [Fact]
        public async Task CrashRecovery_ResetsZombieAndRedeliversOnNextCycle()
        {
            var correlationId = Guid.NewGuid();
            var payload = JsonSerializer.Serialize(new PollerTestMessage { CorrelationId = correlationId });
            var message = OutboxMessage.Create(TestMessageKey, payload);
            var id = await _service.AddAsync(message, CancellationToken);

            // Simulate a crash: a previous poller instance locked this row and never came back.
            message.MarkRunning(DateTime.UtcNow.AddMinutes(-10));
            await _service.UpdateAsync(message, CancellationToken);

            (await GetStatusAsync(id)).ShouldBe(OutboxStatus.Running);

            // ResetZombie's backoff computation clamps to a 1-minute floor (see
            // OutboxMessage.ComputeRetryRunAt: Math.Max(factor, 1.0) minutes), so the row only
            // becomes due again ~1 minute after the zombie-detect tick that reset it — plus up to
            // one 10s poll tick to detect the zombie in the first place, plus another to pick it back
            // up once due. Budget generously for both.
            await WaitUntilAsync(
                async () => await GetStatusAsync(id) == OutboxStatus.Dispatched,
                TimeSpan.FromSeconds(100));

            (await GetStatusAsync(id)).ShouldBe(OutboxStatus.Dispatched);
            ReceivedCorrelationIds.ShouldContain(correlationId);
        }

        [Fact]
        public async Task UnknownMessageTypeKey_DoesNotCrashPoller_MarksRowFailed()
        {
            var badMessage = OutboxMessage.Create($"never-registered-{Guid.NewGuid():N}", "{}");
            var badId = await _service.AddAsync(badMessage, CancellationToken);

            var goodCorrelationId = Guid.NewGuid();
            var goodId = await InsertDueMessageAsync(goodCorrelationId);

            // The good row dispatching proves the bad row didn't take the poller down with it.
            await WaitUntilAsync(
                async () => await GetStatusAsync(goodId) == OutboxStatus.Dispatched,
                TimeSpan.FromSeconds(20));

            (await GetStatusAsync(goodId)).ShouldBe(OutboxStatus.Dispatched);
            ReceivedCorrelationIds.ShouldContain(goodCorrelationId);

            // Default maxRetries (5): one failed attempt leaves it back at Pending (with backoff),
            // not DeadLetter yet — it must never reach Dispatched or Running.
            var badStatus = await GetStatusAsync(badId);
            badStatus.ShouldNotBe(OutboxStatus.Dispatched);
            badStatus.ShouldNotBe(OutboxStatus.Running);
        }

        [Fact]
        public async Task DeadLetterRow_IsNeverPickedUpAgain()
        {
            var message = OutboxMessage.Create($"dead-letter-{Guid.NewGuid():N}", "{}", maxRetries: 0);
            message.Fail("simulated fatal error", DateTime.UtcNow);
            message.Status.ShouldBe(OutboxStatus.DeadLetter);
            await _service.AddAsync(message, CancellationToken);

            var due = await _service.GetDueAsync(batchSize: 50, CancellationToken);

            due.ShouldNotContain(m => m.MessageTypeKey == message.MessageTypeKey);
        }
    }
}
