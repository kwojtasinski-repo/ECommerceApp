using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ECommerceApp.E2E.Backend.Infrastructure
{
    /// <summary>
    /// Base class for real-infrastructure E2E tests. Resolves <typeparamref name="T"/> from a fresh
    /// <see cref="IServiceScope"/> per test instance (xUnit constructs one test class instance per
    /// test method), so each test gets its own DbContext instances/change trackers even though every
    /// test in the collection shares one physical SQL Server database (see <see cref="MsSqlE2EFixture"/>).
    /// Because the database is shared, tests must use unique/random identifiers for the rows they
    /// create rather than fixed literals.
    /// </summary>
    public abstract class SqlServerE2ETestBase<T> : IDisposable where T : notnull
    {
        private readonly IServiceScope _scope;
        protected readonly T Service;

        protected SqlServerE2ETestBase(MsSqlE2EFixture fixture)
        {
            _scope = fixture.Services.CreateScope();
            Service = _scope.ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>Resolves any other service from the same per-test scope (e.g. a second BC's service).</summary>
        protected TService GetRequiredService<TService>() where TService : notnull
            => _scope.ServiceProvider.GetRequiredService<TService>();

        /// <summary>CancellationToken tied to the current xUnit v3 test run.</summary>
        protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

        public void Dispose() => _scope.Dispose();
    }
}
