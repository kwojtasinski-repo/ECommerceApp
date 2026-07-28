using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Database
{
    public sealed class CrossContextTransactionScope : IAsyncDisposable
    {
        private readonly DbContext _primary;
        private readonly IServiceProvider? _serviceProvider;
        private readonly IDbContextTransaction? _transaction;
        private readonly bool _isRelational;
        private bool _completed;

        private CrossContextTransactionScope(
            DbContext primary, IServiceProvider? serviceProvider, IDbContextTransaction? transaction, bool isRelational)
        {
            _primary = primary;
            _serviceProvider = serviceProvider;
            _transaction = transaction;
            _isRelational = isRelational;
        }

        /// <param name="serviceProvider">
        /// Required only when <paramref name="primary"/> uses a non-relational provider (InMemory, in
        /// tests) — <see cref="CreateSecondaryContext{TSecondaryContext}"/> resolves the secondary
        /// context through it in that case. Callers that only ever run against a relational database
        /// (e.g. <c>CrossContextTransactionScopeE2ETests</c>, which talks to a real SQL Server via
        /// Testcontainers directly, bypassing DI) may omit it.
        /// </param>
        public static async Task<CrossContextTransactionScope> BeginAsync(
            DbContext primary, IServiceProvider? serviceProvider = null, CancellationToken ct = default)
        {
            if (primary is null)
                throw new ArgumentNullException(nameof(primary));

            // OpenConnectionAsync/BeginTransactionAsync are meaningless on the InMemory provider that
            // BcWebApplicationFactory/CustomWebApplicationFactory swap in for tests: OpenConnectionAsync
            // is relational-only (throws outright), and InMemory's BeginTransactionAsync throws too —
            // EF Core defaults InMemoryEventId.TransactionIgnoredWarning to "throw" specifically to stop
            // callers relying on transactional semantics the provider can't provide. So for a
            // non-relational provider we skip both entirely and run without a real transaction; there is
            // no atomicity to give up that InMemory could have honored anyway.
            var isRelational = primary.Database.IsRelational();
            if (!isRelational)
            {
                return new CrossContextTransactionScope(primary, serviceProvider, transaction: null, isRelational: false);
            }

            await primary.Database.OpenConnectionAsync(ct);
            var transaction = await primary.Database.BeginTransactionAsync(ct);
            return new CrossContextTransactionScope(primary, serviceProvider, transaction, isRelational: true);
        }

        public TSecondaryContext CreateSecondaryContext<TSecondaryContext>() where TSecondaryContext : DbContext
        {
            if (!_isRelational)
            {
                if (_serviceProvider is null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(CrossContextTransactionScope)} was started against a non-relational " +
                        $"provider without an {nameof(IServiceProvider)}, so {nameof(CreateSecondaryContext)} " +
                        $"has no way to resolve a {typeof(TSecondaryContext).Name} that shares the test " +
                        $"host's InMemory database. Pass the DI service provider to {nameof(BeginAsync)}.");
                }

                // No real connection/transaction to share on a non-relational provider — resolve the
                // secondary context from DI instead of constructing it directly. BcWebApplicationFactory
                // registers each BC's DbContext against a fixed, named InMemory database (Transient
                // lifetime), so a DI-resolved instance still reads/writes the same store as whatever the
                // rest of the test host (e.g. OutboxPollerService) later resolves — unlike a
                // freshly-constructed context pointed at an ad-hoc database, which would be invisible to
                // everyone else.
                return _serviceProvider.GetRequiredService<TSecondaryContext>();
            }

            var sharedConnection = _primary.Database.GetDbConnection();
            var optionsBuilder = new DbContextOptionsBuilder<TSecondaryContext>()
                .UseSqlServer(sharedConnection);

            var secondaryContext = (TSecondaryContext)Activator.CreateInstance(
                typeof(TSecondaryContext), optionsBuilder.Options)!;

            secondaryContext.Database.UseTransaction(_primary.Database.CurrentTransaction!.GetDbTransaction());

            return secondaryContext;
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_transaction is not null)
            {
                await _transaction.CommitAsync(ct);
            }

            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_transaction is not null)
            {
                await _transaction.RollbackAsync(ct);
            }

            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction is null)
            {
                return;
            }

            if (!_completed)
            {
                await _transaction.RollbackAsync();
            }

            await _transaction.DisposeAsync();
            await _primary.Database.CloseConnectionAsync();
        }
    }
}
