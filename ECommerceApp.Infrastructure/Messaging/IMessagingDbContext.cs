using ECommerceApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal interface IMessagingDbContext
    {
        DbSet<OutboxMessage> Outbox { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
