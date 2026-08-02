using System;
using System.Threading.Tasks;
using ECommerceApp.Shared.TestInfrastructure;
using Testcontainers.MsSql;
using Xunit;

namespace ECommerceApp.E2E.Backend.Infrastructure
{
    /// <summary>
    /// E2E fixture: spins up one ephemeral, isolated SQL Server container (via Testcontainers) and
    /// one <see cref="SqlServerE2EWebApplicationFactory"/> host on top of it, shared by every test in
    /// the <c>SqlServerE2E</c> collection. Never touches the application's real
    /// <c>DefaultConnection</c>/<c>ECommerceApp</c> database — this is a throwaway container that
    /// exists only for the lifetime of the test run and is destroyed on <see cref="DisposeAsync"/>.
    /// <para>
    /// The host (and therefore every bounded context's real migrations) is built once per test run,
    /// not once per test — accessing <see cref="Services"/> for the first time triggers host startup.
    /// Individual tests must resolve services from their own <c>IServiceScope</c> (see
    /// <see cref="SqlServerE2ETestBase{T}"/>), not directly from <see cref="Services"/>, and should use
    /// unique/random identifiers for the entities they create since all tests in the collection share
    /// one physical database.
    /// </para>
    /// </summary>
    public sealed class MsSqlE2EFixture : IAsyncLifetime
    {
        private readonly MsSqlContainer _container = new MsSqlBuilder()
            .WithLogger(TestLogging.CreateTestcontainersLogger())
            .WithOutputConsumer(TestLogging.CreateContainerOutputConsumer())
            .Build();
        private SqlServerE2EWebApplicationFactory _factory;

        public IServiceProvider Services => (_factory ?? throw new InvalidOperationException(
            $"{nameof(MsSqlE2EFixture)} has not been initialized yet.")).Services;

        public async ValueTask InitializeAsync()
        {
            await _container.StartAsync();
            _factory = new SqlServerE2EWebApplicationFactory(_container.GetConnectionString());

            // Touching Services builds the host, which runs every BC's real IDbContextMigrator
            // (Database:RunMigrationsOnStart=true) — do this once, eagerly, rather than paying the
            // cost lazily on whichever test happens to resolve a service first.
            _ = _factory.Services;
        }

        public async ValueTask DisposeAsync()
        {
            if (_factory != null)
            {
                await _factory.DisposeAsync();
            }

            await _container.DisposeAsync();
        }
    }

    [CollectionDefinition("SqlServerE2E")]
    public sealed class SqlServerE2ECollection : ICollectionFixture<MsSqlE2EFixture>
    {
    }
}
