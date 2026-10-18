using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using ECommerceApp.E2E.Backend.Messaging;
using ECommerceApp.Infrastructure.Supporting.TimeManagement;
using ECommerceApp.Infrastructure.Supporting.TimeManagement.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.E2E.Backend.TimeManagement
{
    /// <summary>
    /// E2E test — runs against a real SQL Server (see <see cref="CrossContextSqlFixture"/>) because the
    /// lease's guarantee ("exactly one poller instance runs a deferred job row") is about concurrent
    /// writers racing on one physical row, which EF Core's InMemory provider cannot reproduce.
    /// <para>
    /// Every attempt uses its own <see cref="TimeManagementDbContext"/> (own connection, own change
    /// tracker), exactly like each <c>DeferredJobPollerService</c> instance would, and all attempts are
    /// released through a single gate so they hit the row at the same moment.
    /// </para>
    /// <para>
    /// FOR ANYONE ADDING DEFERRED JOB FUNCTIONALITY: this is the only test that exercises the real,
    /// production <c>IDeferredJobInstanceRepository.TryAcquireLeaseAsync</c> (a conditional
    /// <c>ExecuteUpdateAsync</c>). The InMemory test hosts use <c>InMemoryDeferredJobLeaseDecorator</c>
    /// instead, so a regression in the real lease is invisible to them. Read
    /// docs/adr/0009/deferred-job-lease-and-inmemory-test-decorator.md before changing how deferred job
    /// rows are leased, and keep this class green — it is deliberately repeatable.
    /// </para>
    /// </summary>
    [Collection(CrossContextSqlFixture.CollectionName)]
    public class DeferredJobLeaseConcurrencyE2ETests : IAsyncLifetime
    {
        private const int Attempts = 12;
        private const int Repetitions = 25;

        private readonly CrossContextSqlFixture _fixture;
        private readonly List<DeferredJobInstanceId> _createdRows = new();

        public DeferredJobLeaseConcurrencyE2ETests(CrossContextSqlFixture fixture)
        {
            _fixture = fixture;
        }

        public async ValueTask InitializeAsync()
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        // Rows are removed after each test so a leftover due row never competes for the poller's Take(50) batch
        // in a later test.
        public async ValueTask DisposeAsync()
        {
            await using var context = CreateContext();
            var ids = _createdRows.ToList();
            await context.DeferredJobQueue.Where(d => ids.Contains(d.Id)).ExecuteDeleteAsync();
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_DueRowLeasedConcurrently_ExactlyOneAttemptWins()
        {
            var ct = TestContext.Current.CancellationToken;

            for (var repetition = 0; repetition < Repetitions; repetition++)
            {
                var id = await AddDueRowAsync(ct);

                var wins = await AcquireConcurrentlyAsync(id, ct);

                wins.ShouldBe(1, $"repetition {repetition}: {wins} of {Attempts} concurrent leases succeeded for deferred job row {id.Value}");
                (await GetStatusAsync(id, ct)).ShouldBe(DeferredJobStatus.Running);
            }
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_ZombieRowLeasedConcurrently_ExactlyOneAttemptWins()
        {
            var ct = TestContext.Current.CancellationToken;

            for (var repetition = 0; repetition < Repetitions; repetition++)
            {
                var id = await AddDueRowAsync(ct);
                await MakeZombieAsync(id, ct);

                var wins = await AcquireConcurrentlyAsync(id, ct);

                wins.ShouldBe(1, $"repetition {repetition}: {wins} of {Attempts} concurrent leases succeeded for zombie row {id.Value}");
                (await GetStatusAsync(id, ct)).ShouldBe(DeferredJobStatus.Running);
            }
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_RowAlreadyRunningWithLiveLease_IsNotLeased()
        {
            var ct = TestContext.Current.CancellationToken;
            var id = await AddDueRowAsync(ct);

            (await AcquireOnceAsync(id, ct)).ShouldBeTrue();
            (await AcquireOnceAsync(id, ct)).ShouldBeFalse();
        }

        [Fact]
        public async Task TryAcquireLeaseAsync_PendingRowNotYetDue_IsNotLeased()
        {
            var ct = TestContext.Current.CancellationToken;
            var id = await AddRowAsync(DateTime.UtcNow.AddHours(1), ct);

            (await AcquireOnceAsync(id, ct)).ShouldBeFalse();
            (await GetStatusAsync(id, ct)).ShouldBe(DeferredJobStatus.Pending);
        }

        [Fact]
        public async Task PollAsync_ManyPollerInstancesOneDueRow_RowIsDispatchedExactlyOnce()
        {
            var ct = TestContext.Current.CancellationToken;
            const int pollers = 8;

            for (var repetition = 0; repetition < Repetitions; repetition++)
            {
                var id = await AddDueRowAsync(ct);

                // Each poller instance has its own channel, scope factory and DbContext connections, like one
                // host each. A row dispatched by a poller shows up as a request in that poller's channel.
                var instances = Enumerable.Range(0, pollers).Select(_ => CreatePoller()).ToList();
                try
                {
                    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var polls = instances.Select(instance => Task.Run(async () =>
                    {
                        await gate.Task;
                        await instance.Poll(ct);
                    }, ct)).ToList();

                    gate.SetResult();
                    await Task.WhenAll(polls);

                    var dispatches = instances.Sum(instance => CountDispatches(instance.Channel, id));
                    dispatches.ShouldBe(1, $"repetition {repetition}: {dispatches} of {pollers} poller instances dispatched deferred job row {id.Value}");
                }
                finally
                {
                    foreach (var instance in instances)
                    {
                        await instance.Provider.DisposeAsync();
                    }
                }
            }
        }

        [Fact]
        public async Task PollAsync_ZombieRow_IsResetToPendingWithoutDispatchThenDispatchedOnceDue()
        {
            var ct = TestContext.Current.CancellationToken;
            var id = await AddDueRowAsync(ct);
            await MakeZombieAsync(id, ct);
            var instance = CreatePoller();
            try
            {
                // Cycle 1: zombie recovery only. The row goes back to Pending with a backoff and is NOT dispatched.
                await instance.Poll(ct);

                CountDispatches(instance.Channel, id).ShouldBe(0);
                (await GetStatusAsync(id, ct)).ShouldBe(DeferredJobStatus.Pending);

                // Cycle 2 while the backoff has not elapsed: still nothing.
                await instance.Poll(ct);
                CountDispatches(instance.Channel, id).ShouldBe(0);

                // Backoff elapsed: the next cycle dispatches the recovered row.
                await MakeDueAsync(id, ct);
                await instance.Poll(ct);

                CountDispatches(instance.Channel, id).ShouldBe(1);
                (await GetStatusAsync(id, ct)).ShouldBe(DeferredJobStatus.Running);
            }
            finally
            {
                await instance.Provider.DisposeAsync();
            }
        }

        [Fact]
        public async Task PollAsync_ManyPollerInstancesOneZombieRow_RecoveredOnceAndDispatchedExactlyOnceWhenDue()
        {
            var ct = TestContext.Current.CancellationToken;
            const int pollers = 8;

            for (var repetition = 0; repetition < Repetitions; repetition++)
            {
                var id = await AddDueRowAsync(ct);
                await MakeZombieAsync(id, ct);
                var instances = Enumerable.Range(0, pollers).Select(_ => CreatePoller()).ToList();
                try
                {
                    await PollAllConcurrentlyAsync(instances, ct);

                    instances.Sum(i => CountDispatches(i.Channel, id)).ShouldBe(0, $"repetition {repetition}: zombie recovery must not dispatch");
                    (await GetStatusAsync(id, ct)).ShouldBe(DeferredJobStatus.Pending);
                    (await GetRetryCountAsync(id, ct)).ShouldBe(1, $"repetition {repetition}: zombie reset by more than one poller");

                    await MakeDueAsync(id, ct);
                    await PollAllConcurrentlyAsync(instances, ct);

                    var dispatches = instances.Sum(i => CountDispatches(i.Channel, id));
                    dispatches.ShouldBe(1, $"repetition {repetition}: {dispatches} of {pollers} poller instances dispatched recovered row {id.Value}");
                }
                finally
                {
                    foreach (var instance in instances)
                    {
                        await instance.Provider.DisposeAsync();
                    }
                }
            }
        }

        private static async Task PollAllConcurrentlyAsync(IReadOnlyList<PollerInstance> instances, CancellationToken ct)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var polls = instances.Select(instance => Task.Run(async () =>
            {
                await gate.Task;
                await instance.Poll(ct);
            }, ct)).ToList();

            gate.SetResult();
            await Task.WhenAll(polls);
        }

        private async Task MakeDueAsync(DeferredJobInstanceId id, CancellationToken ct)
        {
            await using var context = CreateContext();
            var past = DateTime.UtcNow.AddMinutes(-1);
            await context.DeferredJobQueue.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.RunAt, past), ct);
        }

        private async Task<int> GetRetryCountAsync(DeferredJobInstanceId id, CancellationToken ct)
        {
            await using var context = CreateContext();
            return await context.DeferredJobQueue.AsNoTracking().Where(d => d.Id == id).Select(d => d.RetryCount).SingleAsync(ct);
        }

        private sealed record PollerInstance(ServiceProvider Provider, JobTriggerChannel Channel, Func<CancellationToken, Task> Poll);

        private PollerInstance CreatePoller()
        {
            var channel = new JobTriggerChannel();
            var services = new ServiceCollection();
            services.AddDbContext<TimeManagementDbContext>(options => options.UseSqlServer(_fixture.ConnectionString));
            services.AddScoped<ITimeManagementDbContext>(sp => sp.GetRequiredService<TimeManagementDbContext>());
            services.AddScoped<IDeferredJobInstanceRepository, DeferredJobInstanceRepository>();
            var provider = services.BuildServiceProvider();

            var poller = new DeferredJobPollerService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                channel,
                NullLogger<DeferredJobPollerService>.Instance);
            var pollAsync = typeof(DeferredJobPollerService).GetMethod("PollAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

            return new PollerInstance(provider, channel, ct => (Task)pollAsync.Invoke(poller, new object[] { ct })!);
        }

        private static int CountDispatches(JobTriggerChannel channel, DeferredJobInstanceId id)
        {
            var count = 0;
            while (channel.Reader.TryRead(out var request))
            {
                if (request.DeferredInstanceId == id.Value)
                {
                    count++;
                }
            }

            return count;
        }

        private async Task<int> AcquireConcurrentlyAsync(DeferredJobInstanceId id, CancellationToken ct)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ready = 0;

            var attempts = Enumerable.Range(0, Attempts).Select(_ => Task.Run(async () =>
            {
                await using var context = CreateContext();
                var repository = new DeferredJobInstanceRepository(context);

                // Open the connection before the gate so the race is on the lease itself, not on pool warm-up.
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

        private async Task<bool> AcquireOnceAsync(DeferredJobInstanceId id, CancellationToken ct)
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            return await new DeferredJobInstanceRepository(context).TryAcquireLeaseAsync(id, now, now.AddMinutes(5), ct);
        }

        private Task<DeferredJobInstanceId> AddDueRowAsync(CancellationToken ct)
        {
            return AddRowAsync(DateTime.UtcNow.AddMinutes(-5), ct);
        }

        private async Task<DeferredJobInstanceId> AddRowAsync(DateTime runAt, CancellationToken ct)
        {
            await using var context = CreateContext();
            var instance = DeferredJobInstance.Schedule("DeferredJobLeaseTest", Guid.NewGuid().ToString("N"), runAt);
            context.DeferredJobQueue.Add(instance);
            await context.SaveChangesAsync(ct);
            _createdRows.Add(instance.Id);
            return instance.Id;
        }

        private async Task MakeZombieAsync(DeferredJobInstanceId id, CancellationToken ct)
        {
            await using var context = CreateContext();
            var instance = await context.DeferredJobQueue.SingleAsync(d => d.Id == id, ct);
            instance.MarkRunning(DateTime.UtcNow.AddMinutes(-10));
            await context.SaveChangesAsync(ct);
        }

        private async Task<DeferredJobStatus> GetStatusAsync(DeferredJobInstanceId id, CancellationToken ct)
        {
            await using var context = CreateContext();
            return await context.DeferredJobQueue.AsNoTracking().Where(d => d.Id == id).Select(d => d.Status).SingleAsync(ct);
        }

        private TimeManagementDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TimeManagementDbContext>().UseSqlServer(_fixture.ConnectionString).Options;
            return new TimeManagementDbContext(options);
        }
    }
}
