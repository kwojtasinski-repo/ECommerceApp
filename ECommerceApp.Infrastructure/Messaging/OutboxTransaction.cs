using ECommerceApp.Application.Messaging;
using ECommerceApp.Infrastructure.Database;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class OutboxTransaction : IOutboxTransaction
    {
        internal CrossContextTransactionScope Scope { get; }

        public OutboxTransaction(CrossContextTransactionScope scope)
        {
            Scope = scope;
        }

        public Task CommitAsync(CancellationToken ct = default) => Scope.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct = default) => Scope.RollbackAsync(ct);

        public ValueTask DisposeAsync() => Scope.DisposeAsync();
    }
}

