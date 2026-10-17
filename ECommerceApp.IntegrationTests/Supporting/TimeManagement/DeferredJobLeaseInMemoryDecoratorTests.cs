using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using ECommerceApp.Infrastructure.Supporting.TimeManagement;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Supporting.TimeManagement
{
    /// <summary>
    /// FOR ANYONE ADDING DEFERRED JOB FUNCTIONALITY — read
    /// docs/adr/0009/deferred-job-lease-and-inmemory-test-decorator.md.
    /// <para>
    /// These tests exist on purpose and are meant to be re-run (or copied) whenever the deferred job lease
    /// changes. InMemory hosts cannot run the production lease (<c>ExecuteUpdateAsync</c>), so
    /// <see cref="BcWebApplicationFactory"/> decorates <see cref="IDeferredJobInstanceRepository"/> with a
    /// test-only stand-in. They prove two things: (1) the decoration is actually active — if it is not, the
    /// poller's lease throws on InMemory and the poller silently runs nothing; (2) the stand-in keeps the
    /// exactly-one-winner contract.
    /// </para>
    /// <para>
    /// They do NOT prove the production behaviour. That is only covered by
    /// <c>DeferredJobLeaseConcurrencyE2ETests</c> in ECommerceApp.E2E.Backend (real SQL Server).
    /// </para>
    /// <para>
    /// Rows are scheduled 30 minutes ahead (or parked Running with a lease 30 minutes ahead) and leased with a
    /// lease time one hour ahead, so they are DUE or ZOMBIE only from the test's point of view and the host's
    /// real <c>DeferredJobPollerService</c> leaves them alone.
    /// </para>
    /// </summary>
    public class DeferredJobLeaseInMemoryDecoratorTests : BcBaseTest<IDeferredJobInstanceRepository>
    {
        private const int Attempts = 12;
        private const int Repetitions = 25;

        public DeferredJobLeaseInMemoryDecoratorTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void DeferredJobRepository_InInMemoryHost_IsTheLeaseDecorator()
        {
            using var scope = Services.CreateScope();

            var repository = scope.ServiceProvider.GetRequiredService<IDeferredJobInstanceRepository>();

            repository.GetType().Name.ShouldBe("InMemoryDeferredJobLeaseDecorator");
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_ConcurrentLeasesOnDueRow_ExactlyOneAttemptWins()
        {
            for (var repetition = 0; repetition < Repetitions; repetition++)
            {
                var id = await AddRowAsync();
                var leaseTime = DateTime.UtcNow.AddHours(1);

                var wins = await AcquireConcurrentlyAsync(id, leaseTime);

                wins.ShouldBe(1, $"repetition {repetition}: {wins} of {Attempts} concurrent leases succeeded for deferred job row {id.Value}");
            }
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_ConcurrentLeasesOnZombieRow_ExactlyOneAttemptWins()
        {
            for (var repetition = 0; repetition < Repetitions; repetition++)
            {
                var id = await AddRowAsync();
                await ParkRunningAsync(id);
                var leaseTime = DateTime.UtcNow.AddHours(1);

                var wins = await AcquireConcurrentlyAsync(id, leaseTime);

                wins.ShouldBe(1, $"repetition {repetition}: {wins} of {Attempts} concurrent leases succeeded for zombie row {id.Value}");
            }
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_RowWithLiveLease_IsNotLeased()
        {
            var id = await AddRowAsync();
            await ParkRunningAsync(id);

            (await AcquireOnceAsync(id, DateTime.UtcNow)).ShouldBeFalse();
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_PendingRowNotYetDue_IsNotLeased()
        {
            var id = await AddRowAsync();

            (await AcquireOnceAsync(id, DateTime.UtcNow)).ShouldBeFalse();
        }

        private async Task<DeferredJobInstanceId> AddRowAsync()
        {
            using var scope = Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDeferredJobInstanceRepository>();

            var instance = DeferredJobInstance.Schedule("DeferredJobLeaseTest", Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(30));
            await repository.AddAsync(instance, CancellationToken);
            return instance.Id;
        }

        private async Task ParkRunningAsync(DeferredJobInstanceId id)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ITimeManagementDbContext>();

            var instance = await context.DeferredJobQueue.SingleAsync(d => d.Id == id, CancellationToken);
            instance.MarkRunning(DateTime.UtcNow.AddMinutes(30));
            await context.SaveChangesAsync(CancellationToken);
        }

        private async Task<bool> AcquireOnceAsync(DeferredJobInstanceId id, DateTime leaseTime)
        {
            using var scope = Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDeferredJobInstanceRepository>();
            return await repository.TryAcquireLeaseAsync(id, leaseTime, leaseTime.AddMinutes(5), CancellationToken);
        }

        private async Task<int> AcquireConcurrentlyAsync(DeferredJobInstanceId id, DateTime leaseTime)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ready = 0;

            var attempts = Enumerable.Range(0, Attempts).Select(_ => Task.Run(async () =>
            {
                using var scope = Services.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IDeferredJobInstanceRepository>();

                Interlocked.Increment(ref ready);
                await gate.Task;

                return await repository.TryAcquireLeaseAsync(id, leaseTime, leaseTime.AddMinutes(5), CancellationToken);
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
