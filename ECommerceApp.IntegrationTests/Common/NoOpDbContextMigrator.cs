using ECommerceApp.Infrastructure.Database;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.IntegrationTests.Common
{
    /// <summary>
    /// No-op migrator for InMemory databases. <see cref="IDbContextMigrator.MigrateAsync"/>
    /// is not supported by the InMemory provider.
    /// </summary>
    internal sealed class NoOpDbContextMigrator : IDbContextMigrator
    {
        public string ContextName => "InMemory";

        public Task MigrateAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
