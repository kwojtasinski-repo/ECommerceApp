using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Messaging
{
    public interface IModuleClient
    {
        Task PublishAsync(IMessage message);
        Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
    }
}
