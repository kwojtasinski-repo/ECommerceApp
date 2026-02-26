using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IMessageHandler<TMessage> where TMessage : class, IMessage
    {
        Task HandleAsync(TMessage message, CancellationToken ct = default);
    }
}
