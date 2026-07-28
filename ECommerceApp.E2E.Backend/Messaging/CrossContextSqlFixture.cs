using System.Threading.Tasks;
using Testcontainers.MsSql;
using Xunit;

namespace ECommerceApp.E2E.Backend.Messaging
{
    /// <summary>
    /// E2E fixture: spins up one ephemeral, isolated SQL Server container (via Testcontainers)
    /// shared by every test in the <c>CrossContextSqlServer</c> collection. Never touches the
    /// application's real <c>DefaultConnection</c>/<c>ECommerceApp</c> database — this is a
    /// throwaway container that exists only for the lifetime of the test run and is destroyed on
    /// <see cref="DisposeAsync"/>.
    /// <para>
    /// Needed because <c>CrossContextTransactionScope</c> hardcodes <c>UseSqlServer(...)</c> and its
    /// commit/rollback proof depends on real ADO.NET connection/transaction sharing
    /// (<c>OpenConnectionAsync</c>/<c>GetDbConnection</c>/<c>UseTransaction</c>) — none of which EF
    /// Core's InMemory provider can exercise. Deliberately a bare container with no DI host (unlike
    /// <see cref="Infrastructure.MsSqlE2EFixture"/>): <see cref="CrossContextTransactionScopeE2ETests"/>
    /// constructs <c>DbContext</c> instances directly against the container's connection string,
    /// bypassing DI entirely — see <c>CrossContextTransactionScope.BeginAsync</c>'s XML doc.
    /// </para>
    /// </summary>
    public sealed class CrossContextSqlFixture : IAsyncLifetime
    {
        private readonly MsSqlContainer _container = new MsSqlBuilder().Build();

        public string ConnectionString => _container.GetConnectionString();

        public async ValueTask InitializeAsync() => await _container.StartAsync();

        public async ValueTask DisposeAsync() => await _container.DisposeAsync();
    }

    [CollectionDefinition("CrossContextSqlServer")]
    public sealed class CrossContextSqlServerCollection : ICollectionFixture<CrossContextSqlFixture>
    {
    }
}
