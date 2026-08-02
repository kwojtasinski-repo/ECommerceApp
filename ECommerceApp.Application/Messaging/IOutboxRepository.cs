using ECommerceApp.Domain.Messaging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IOutboxRepository
    {
        Task<long> AddAsync(OutboxMessage message, CancellationToken ct = default);
        Task<IReadOnlyList<OutboxMessage>> GetDueAsync(int batchSize, CancellationToken ct = default);
        Task UpdateAsync(OutboxMessage message, CancellationToken ct = default);
        Task<int> DeleteDispatchedOlderThanAsync(System.DateTime cutoff, CancellationToken ct = default);
    }
}
