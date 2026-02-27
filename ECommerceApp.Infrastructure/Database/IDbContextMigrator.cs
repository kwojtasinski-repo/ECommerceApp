using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Database
{
    internal interface IDbContextMigrator
    {
        Task MigrateAsync(CancellationToken ct = default);
    }
}
