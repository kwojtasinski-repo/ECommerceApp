using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Messaging
{
    /// <summary>
    /// FOR ANYONE ADDING OUTBOX FUNCTIONALITY — read docs/adr/0010/outbox-claim-and-inmemory-test-decorator.md.
    /// <para>
    /// These tests exist on purpose and are meant to be re-run (or copied) whenever the Outbox claim changes.
    /// InMemory hosts cannot run the production claim (<c>ExecuteUpdateAsync</c>), so
    /// <see cref="BcWebApplicationFactory"/> decorates <see cref="IOutboxRepository"/> with a test-only
    /// stand-in. They prove two things: (1) the decoration is actually active — if it is not, the poller's
    /// claim throws on InMemory and the poller silently dispatches nothing, so hosts just hang; (2) the
    /// stand-in keeps the exactly-one-winner contract.
    /// </para>
    /// <para>
    /// They do NOT prove the production behaviour. That is only covered by
    /// <c>OutboxClaimConcurrencyE2ETests</c> in ECommerceApp.E2E.Backend (real SQL Server).
    /// </para>
    /// <para>
    /// The row is parked as <c>Running</c> with a lock that is live for the running poller but expired for
    /// the claim time used here, so the host's real <c>OutboxPollerService</c> leaves it alone.
    /// </para>
    /// </summary>
    public class OutboxClaimInMemoryDecoratorTests : BcBaseTest<IOutboxRepository>
    {
        private const int Attempts = 12;
        private const int Repetitions = 25;

        public OutboxClaimInMemoryDecoratorTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void OutboxRepository_InInMemoryHost_IsTheClaimDecorator()
        {
            using var scope = Services.CreateScope();

            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

            repository.GetType().Name.ShouldBe("InMemoryOutboxClaimDecorator");
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_ConcurrentClaims_ExactlyOneAttemptWins()
        {
            for (var repetition = 0; repetition < Repetitions; repetition++)
            {
                var id = await AddParkedRowAsync();
                var claimTime = DateTime.UtcNow.AddHours(1);

                var wins = await ClaimConcurrentlyAsync(id, claimTime);

                wins.ShouldBe(1, $"repetition {repetition}: {wins} of {Attempts} concurrent claims succeeded for outbox row {id}");
            }
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_RowWithLiveLock_IsNotClaimed()
        {
            var id = await AddParkedRowAsync();

            (await ClaimOnceAsync(id, DateTime.UtcNow)).ShouldBeFalse();
        }

        private async Task<long> AddParkedRowAsync()
        {
            using var scope = Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

            var message = OutboxMessage.Create($"claim-decorator-test-{Guid.NewGuid():N}", "{}");
            var id = await repository.AddAsync(message, CancellationToken);

            message.MarkRunning(DateTime.UtcNow.AddMinutes(30));
            await repository.UpdateAsync(message, CancellationToken);
            return id;
        }

        private async Task<bool> ClaimOnceAsync(long id, DateTime claimTime)
        {
            using var scope = Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            return await repository.TryAcquireLeaseAsync(id, claimTime, claimTime.AddMinutes(5), CancellationToken);
        }

        private async Task<int> ClaimConcurrentlyAsync(long id, DateTime claimTime)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ready = 0;

            var attempts = Enumerable.Range(0, Attempts).Select(_ => Task.Run(async () =>
            {
                using var scope = Services.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

                Interlocked.Increment(ref ready);
                await gate.Task;

                return await repository.TryAcquireLeaseAsync(id, claimTime, claimTime.AddMinutes(5), CancellationToken);
            })).ToList();

            while (Volatile.Read(ref ready) < Attempts)
            {
                await Task.Delay(5, CancellationToken);
            }

            gate.SetResult();
            var results = await Task.WhenAll(attempts);
            return results.Count(won => won);
        }
    }
}
