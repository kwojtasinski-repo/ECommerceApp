using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IModuleClient
    {
        Task PublishAsync(IMessage message);
    }
}
