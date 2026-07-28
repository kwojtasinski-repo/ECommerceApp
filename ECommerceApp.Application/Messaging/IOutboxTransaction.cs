using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    /// <summary>
    /// Application-visible handle for an open cross-context transaction, hiding
    /// <c>ECommerceApp.Infrastructure.Database.CrossContextTransactionScope</c> completely.
    /// </summary>
    public interface IOutboxTransaction : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken ct = default);
        Task RollbackAsync(CancellationToken ct = default);
    }
}
