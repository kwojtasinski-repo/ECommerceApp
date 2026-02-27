using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Database
{
    internal sealed class DbContextMigrator<TContext> : IDbContextMigrator
        where TContext : DbContext
    {
        private readonly TContext _context;

        public DbContextMigrator(TContext context)
        {
            _context = context;
        }

        public string ContextName => typeof(TContext).Name;

        public Task MigrateAsync(CancellationToken ct = default)
            => _context.Database.MigrateAsync(ct);
    }
}
