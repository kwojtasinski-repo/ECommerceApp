using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IMessageBroker
    {
        Task PublishAsync(params IMessage[] messages);
    }
}
