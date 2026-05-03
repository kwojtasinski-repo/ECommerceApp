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

        public Task MigrateAsync(CancellationToken ct = default)
            => _context.Database.MigrateAsync(ct);
    }
}
