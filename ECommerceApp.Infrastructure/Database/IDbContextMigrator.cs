using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Database
{
    public interface IDbContextMigrator
    {
        string ContextName { get; }
        Task MigrateAsync(CancellationToken ct = default);
    }
}
