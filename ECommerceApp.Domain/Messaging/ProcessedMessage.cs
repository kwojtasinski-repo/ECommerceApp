using System;
using ECommerceApp.Domain.Shared;

namespace ECommerceApp.Domain.Messaging
{
    public class ProcessedMessage
    {
        public long MessageId { get; private set; }
        public string HandlerType { get; private set; } = default!;
        public DateTime ProcessedAt { get; private set; }

        private ProcessedMessage() { }

        public static ProcessedMessage Create(long messageId, string handlerType)
        {
            if (messageId <= 0)
                throw new DomainException("Message id must be positive.");
            if (string.IsNullOrWhiteSpace(handlerType))
                throw new DomainException("Handler type must not be empty.");

            return new ProcessedMessage
            {
                MessageId = messageId,
                HandlerType = handlerType,
                ProcessedAt = DateTime.UtcNow
            };
        }
    }
}