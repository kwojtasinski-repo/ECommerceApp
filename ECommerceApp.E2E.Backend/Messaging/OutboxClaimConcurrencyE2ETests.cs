using ECommerceApp.Domain.Messaging;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Infrastructure.Messaging.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.E2E.Backend.Messaging
{
    /// <summary>
    /// E2E test — runs against a real SQL Server (see <see cref="CrossContextSqlFixture"/>) because the
    /// claim's guarantee ("exactly one poller wins a row") is about concurrent writers racing on one
    /// physical row, which EF Core's InMemory provider cannot reproduce.
    /// <para>
    /// Every attempt uses its own <see cref="MessagingDbContext"/> (own connection, own change tracker),
    /// exactly like each <c>OutboxPollerService</c> instance (or each per-message scope) would, and all
    /// attempts are released through a single gate so they hit the row at the same moment.
    /// </para>
    /// <para>
    /// FOR ANYONE ADDING OUTBOX FUNCTIONALITY: this is the only test that exercises the real, production
    /// <c>IOutboxRepository.TryAcquireLeaseAsync</c> (a conditional <c>ExecuteUpdateAsync</c>). The
    /// InMemory test hosts use <c>InMemoryOutboxClaimDecorator</c> instead, so a regression in the real
    /// claim is invisible to them. Read docs/adr/0010/outbox-claim-and-inmemory-test-decorator.md before
    /// changing how Outbox rows are claimed, and keep this class green — it is deliberately repeatable.
    /// </para>
    /// </summary>
    [Collection(CrossContextSqlFixture.CollectionName)]
    public class OutboxClaimConcurrencyE2ETests : IAsyncLifetime
    {
        private const int Attempts = 12;
        private const int Repetitions = 25;

        private readonly CrossContextSqlFixture _fixture;

        public OutboxClaimConcurrencyE2ETests(CrossContextSqlFixture fixture)
        {
            _fixture = fixture;
        }

        public async ValueTask InitializeAsync()
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        [Fact]
        public async Task TryAcquireLeaseAsync_PendingRowClaimedConcurrently_ExactlyOneAttemptWins()
        {
            var ct = TestContext.Current.CancellationToken;

            for (var repetition = 0; repetition < Repetitions; repetition++)
            {
                var id = await AddRowAsync(ct);

                var wins = await ClaimConcurrentlyAsync(id, ct);

                wins.ShouldBe(1, $"repetition {repetition}: {wins} of {Attempts} concurrent claims succeeded for outbox row {id}");
                (await GetStatusAsync(id, ct)).ShouldBe(OutboxStatus.Running);
            }
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_ZombieRowClaimedConcurrently_ExactlyOneAttemptWins()
        {
            var ct = TestContext.Current.CancellationToken;

            for (var repetition = 0; repetition < Repetitions; repetition++)
            {
                var id = await AddRowAsync(ct);
                await MakeZombieAsync(id, ct);

                var wins = await ClaimConcurrentlyAsync(id, ct);

                wins.ShouldBe(1, $"repetition {repetition}: {wins} of {Attempts} concurrent claims succeeded for zombie row {id}");
                (await GetStatusAsync(id, ct)).ShouldBe(OutboxStatus.Running);
            }
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_RowAlreadyRunningWithLiveLock_IsNotClaimed()
        {
            var ct = TestContext.Current.CancellationToken;
            var id = await AddRowAsync(ct);

            (await ClaimOnceAsync(id, ct)).ShouldBeTrue();
            (await ClaimOnceAsync(id, ct)).ShouldBeFalse();
        }

        private async Task<int> ClaimConcurrentlyAsync(long id, CancellationToken ct)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ready = 0;

            var attempts = Enumerable.Range(0, Attempts).Select(_ => Task.Run(async () =>
            {
                await using var context = CreateContext();
                var repository = new OutboxRepository(context);

                // Open the connection before the gate so the race is on the claim itself, not on pool warm-up.
                await context.Database.OpenConnectionAsync(ct);
                Interlocked.Increment(ref ready);
                await gate.Task;

                var now = DateTime.UtcNow;
                return await repository.TryAcquireLeaseAsync(id, now, now.AddMinutes(5), ct);
            }, ct)).ToList();

            while (Volatile.Read(ref ready) < Attempts)
            {
                await Task.Delay(5, ct);
            }

            gate.SetResult();
            var results = await Task.WhenAll(attempts);
            return results.Count(won => won);
        }

        private async Task<bool> ClaimOnceAsync(long id, CancellationToken ct)
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            return await new OutboxRepository(context).TryAcquireLeaseAsync(id, now, now.AddMinutes(5), ct);
        }

        private async Task<long> AddRowAsync(CancellationToken ct)
        {
            await using var context = CreateContext();
            var message = OutboxMessage.Create($"claim-test-{Guid.NewGuid():N}", "{}");
            context.Outbox.Add(message);
            await context.SaveChangesAsync(ct);
            return message.Id;
        }

        private async Task MakeZombieAsync(long id, CancellationToken ct)
        {
            await using var context = CreateContext();
            var message = await context.Outbox.SingleAsync(m => m.Id == id, ct);
            message.MarkRunning(DateTime.UtcNow.AddMinutes(-10));
            await context.SaveChangesAsync(ct);
        }

        private async Task<OutboxStatus> GetStatusAsync(long id, CancellationToken ct)
        {
            await using var context = CreateContext();
            return await context.Outbox.AsNoTracking().Where(m => m.Id == id).Select(m => m.Status).SingleAsync(ct);
        }

        private MessagingDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<MessagingDbContext>().UseSqlServer(_fixture.ConnectionString).Options;
            return new MessagingDbContext(options);
        }
    }
}
