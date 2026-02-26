using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IAsyncMessageDispatcher
    {
        Task PublishAsync<TMessage>(TMessage message) where TMessage : class, IMessage;
    }
}
