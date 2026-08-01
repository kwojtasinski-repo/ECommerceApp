using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IIdAwareMessageHandler<TMessage> : IMessageHandler<TMessage>
        where TMessage : class, IMessage
    {
        Task HandleAsync(TMessage message, long outboxMessageId, CancellationToken ct = default);
    }
}