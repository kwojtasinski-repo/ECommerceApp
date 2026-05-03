using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Database
{
    internal sealed class DbContextMigrator<TContext> : IDbContextMigrator
        where TContext : DbContext
    {
        private readonly TContext _context;
        private readonly ILogger<DbContextMigrator<TContext>> _logger;

        public DbContextMigrator(TContext context, ILogger<DbContextMigrator<TContext>> logger)
        {
            _context = context;
            _logger = logger;
        }

        public string ContextName => typeof(TContext).Name;

        public async Task MigrateAsync(CancellationToken ct = default)
        {
            var migrations = await _context.Database.GetPendingMigrationsAsync(ct);
            // logger log it
            foreach (var migration in migrations)
            {
                _logger.LogInformation("Pending migration for {Context}: {Migration}", ContextName, migration);
            }

            var script = _context.Database.GenerateCreateScript();
            // logger log it
            _logger.LogInformation("Applying migrations for {Context}: {Migrations}", ContextName, string.Join(", ", script));

            await _context.Database.MigrateAsync(ct);
        }
    }
}
