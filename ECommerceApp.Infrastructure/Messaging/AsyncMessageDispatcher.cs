using ECommerceApp.Application.Messaging;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class AsyncMessageDispatcher : IAsyncMessageDispatcher
    {
        private readonly IMessageChannel _channel;

        public AsyncMessageDispatcher(IMessageChannel channel)
        {
            _channel = channel;
        }

        public Task PublishAsync<TMessage>(TMessage message) where TMessage : class, IMessage
            => _channel.Writer.WriteAsync(message).AsTask();
    }
}
