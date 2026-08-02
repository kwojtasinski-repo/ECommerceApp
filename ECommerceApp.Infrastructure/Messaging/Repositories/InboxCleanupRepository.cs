using ECommerceApp.Application.Messaging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging.Repositories
{
    internal sealed class InboxCleanupRepository : IInboxCleanupRepository
    {
        private readonly IMessagingDbContext _context;

        public InboxCleanupRepository(IMessagingDbContext context)
        {
            _context = context;
        }

        public async Task<int> DeleteProcessedOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
        {
            var messages = await _context.Inbox
                .Where(m => m.ProcessedAt < cutoff)
                .ToListAsync(ct);

            _context.Inbox.RemoveRange(messages);
            await _context.SaveChangesAsync(ct);
            return messages.Count;
        }
    }
}