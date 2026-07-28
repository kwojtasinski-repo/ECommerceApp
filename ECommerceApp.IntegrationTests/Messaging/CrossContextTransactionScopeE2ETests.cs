using ECommerceApp.Domain.Messaging;
using ECommerceApp.Domain.Supporting.TimeManagement;
using ECommerceApp.Domain.Supporting.TimeManagement.ValueObjects;
using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Infrastructure.Supporting.TimeManagement;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Messaging
{
    /// <summary>
    /// E2E test — the only test class in this suite that runs against a real database engine
    /// instead of EF Core's InMemory provider (see <see cref="MsSqlE2EFixture"/>). Everything else
    /// under <c>ECommerceApp.IntegrationTests</c> is a regular (InMemory-backed) integration test.
    /// <para>
    /// Correctness proof for <see cref="CrossContextTransactionScope"/> — the entire outbox-atomicity
    /// claim rests on this: a rollback must undo writes made through BOTH the primary and secondary
    /// <see cref="DbContext"/> together, and a commit must persist BOTH together, because they share one
    /// physical ADO.NET connection/transaction.
    /// </para>
    /// <para>
    /// Runs against a real, ephemeral SQL Server container (<see cref="MsSqlE2EFixture"/>, via
    /// Testcontainers) because <see cref="CrossContextTransactionScope.CreateSecondaryContext{TSecondaryContext}"/>
    /// hardcodes <c>UseSqlServer(...)</c> per this phase's design (every BC's <c>DbContext</c> points at
    /// one physical SQL Server database, split by schema) — InMemory has no real
    /// <see cref="System.Data.Common.DbConnection"/>, so <c>OpenConnectionAsync</c>/<c>GetDbConnection</c>/
    /// <c>UseTransaction</c> (the exact mechanism under test) are not supported by it at all. The
    /// container is isolated and throwaway — it is never the application's real <c>DefaultConnection</c>.
    /// </para>
    /// </summary>
    [Collection("CrossContextSqlServer")]
    public class CrossContextTransactionScopeE2ETests : IAsyncLifetime
    {
        private readonly MsSqlE2EFixture _fixture;

        public CrossContextTransactionScopeE2ETests(MsSqlE2EFixture fixture)
        {
            _fixture = fixture;
        }

        public async ValueTask InitializeAsync()
        {
            // Deliberately MigrateAsync, not EnsureCreatedAsync: EnsureCreated only provisions
            // tables when the *database itself* doesn't exist yet — the first EnsureCreatedAsync
            // call (for MessagingDbContext) creates the container's single physical database, and
            // a second EnsureCreatedAsync call for a different DbContext (TimeManagementDbContext)
            // against that now-already-existing database silently no-ops, leaving its tables
            // missing ("Invalid object name 'time_management.ScheduledJobs'" — caught by this test
            // suite the first time it ran for real against SQL Server instead of InMemory).
            // MigrateAsync is also what production actually uses (IDbContextMigrator), so this is
            // more representative besides being correct.
            await using var messagingContext = CreateContext<MessagingDbContext>(_fixture.ConnectionString);
            await using var timeManagementContext = CreateContext<TimeManagementDbContext>(_fixture.ConnectionString);
            await messagingContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await timeManagementContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        [Fact]
        public async Task RollbackAsync_RollsBackBothContexts()
        {
            var ct = TestContext.Current.CancellationToken;
            var jobName = new JobName($"CrossContextTest_{Guid.NewGuid():N}");
            var messageKey = $"cross-context-test-{Guid.NewGuid():N}";

            await using var messagingContext = CreateContext<MessagingDbContext>(_fixture.ConnectionString);
            await using var timeManagementContext = CreateContext<TimeManagementDbContext>(_fixture.ConnectionString);

            await using (var scope = await CrossContextTransactionScope.BeginAsync(messagingContext, ct: ct))
            {
                messagingContext.Outbox.Add(OutboxMessage.Create(messageKey, "{}"));
                await messagingContext.SaveChangesAsync(ct);

                await using var secondary = scope.CreateSecondaryContext<TimeManagementDbContext>();
                secondary.ScheduledJobs.Add(ScheduledJob.Create(jobName.Value, "0 * * * *", null, 3));
                await secondary.SaveChangesAsync(ct);

                await scope.RollbackAsync(ct);
            }

            (await messagingContext.Outbox.AnyAsync(m => m.MessageTypeKey == messageKey, ct)).ShouldBeFalse();
            (await timeManagementContext.ScheduledJobs.AnyAsync(j => j.Name == jobName, ct)).ShouldBeFalse();
        }

        [Fact]
        public async Task CommitAsync_PersistsBothContexts()
        {
            var ct = TestContext.Current.CancellationToken;
            var jobName = new JobName($"CrossContextTest_{Guid.NewGuid():N}");
            var messageKey = $"cross-context-test-{Guid.NewGuid():N}";

            await using var messagingContext = CreateContext<MessagingDbContext>(_fixture.ConnectionString);
            await using var timeManagementContext = CreateContext<TimeManagementDbContext>(_fixture.ConnectionString);

            await using (var scope = await CrossContextTransactionScope.BeginAsync(messagingContext, ct: ct))
            {
                messagingContext.Outbox.Add(OutboxMessage.Create(messageKey, "{}"));
                await messagingContext.SaveChangesAsync(ct);

                await using var secondary = scope.CreateSecondaryContext<TimeManagementDbContext>();
                secondary.ScheduledJobs.Add(ScheduledJob.Create(jobName.Value, "0 * * * *", null, 3));
                await secondary.SaveChangesAsync(ct);

                await scope.CommitAsync(ct);
            }

            (await messagingContext.Outbox.AnyAsync(m => m.MessageTypeKey == messageKey, ct)).ShouldBeTrue();
            (await timeManagementContext.ScheduledJobs.AnyAsync(j => j.Name == jobName, ct)).ShouldBeTrue();
        }

        [Fact]
        public async Task DisposeWithoutCommitOrRollback_RollsBackBothContexts()
        {
            // Fail-safe proof: an un-committed scope must never silently commit on dispose (see
            // CrossContextTransactionScope.DisposeAsync). Mirrors RollbackAsync_RollsBackBothContexts
            // exactly, except the transaction is discarded via the implicit `await using` dispose
            // instead of an explicit RollbackAsync() call.
            var ct = TestContext.Current.CancellationToken;
            var jobName = new JobName($"CrossContextTest_{Guid.NewGuid():N}");
            var messageKey = $"cross-context-test-{Guid.NewGuid():N}";

            await using var messagingContext = CreateContext<MessagingDbContext>(_fixture.ConnectionString);
            await using var timeManagementContext = CreateContext<TimeManagementDbContext>(_fixture.ConnectionString);

            await using (var scope = await CrossContextTransactionScope.BeginAsync(messagingContext, ct: ct))
            {
                messagingContext.Outbox.Add(OutboxMessage.Create(messageKey, "{}"));
                await messagingContext.SaveChangesAsync(ct);

                await using var secondary = scope.CreateSecondaryContext<TimeManagementDbContext>();
                secondary.ScheduledJobs.Add(ScheduledJob.Create(jobName.Value, "0 * * * *", null, 3));
                await secondary.SaveChangesAsync(ct);

                // Deliberately no CommitAsync()/RollbackAsync() call — the `await using` block's
                // implicit DisposeAsync() must roll back on its own.
            }

            (await messagingContext.Outbox.AnyAsync(m => m.MessageTypeKey == messageKey, ct)).ShouldBeFalse();
            (await timeManagementContext.ScheduledJobs.AnyAsync(j => j.Name == jobName, ct)).ShouldBeFalse();
        }

        private static TContext CreateContext<TContext>(string connectionString) where TContext : DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<TContext>().UseSqlServer(connectionString);
            return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
        }
    }
}
