using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Messaging;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class ProcessedMessageGuard : IProcessedMessageGuard
    {
        private readonly MessagingDbContext _messagingContext;

        public ProcessedMessageGuard(MessagingDbContext messagingContext)
        {
            _messagingContext = messagingContext;
        }

        public async Task<bool> TryMarkProcessedAsync(
            long messageId,
            string handlerType,
            IOutboxTransaction transaction,
            CancellationToken ct = default)
        {
            if (transaction is not OutboxTransaction concrete)
            {
                throw new InvalidOperationException(
                    $"{nameof(IOutboxTransaction)} passed to {nameof(ProcessedMessageGuard)} " +
                    $"must have been created by the messaging infrastructure.");
            }

            await using var messagingContext = concrete.Scope.CreateSecondaryContext<MessagingDbContext>();
            return await TrySaveAsync(messagingContext, messageId, handlerType, ct);
        }

        public Task<bool> TryMarkProcessedAsync(
            long messageId,
            string handlerType,
            CancellationToken ct = default)
            => TrySaveAsync(_messagingContext, messageId, handlerType, ct);

        private static async Task<bool> TrySaveAsync(
            MessagingDbContext messagingContext,
            long messageId,
            string handlerType,
            CancellationToken ct)
        {
            messagingContext.Inbox.Add(ProcessedMessage.Create(messageId, handlerType));

            try
            {
                await messagingContext.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return false;
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException
                && (sqlException.Number == 2601 || sqlException.Number == 2627);
        }
    }
}