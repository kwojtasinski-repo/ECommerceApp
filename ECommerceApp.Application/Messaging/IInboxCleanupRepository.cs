using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IInboxCleanupRepository
    {
        Task<int> DeleteProcessedOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
    }
}