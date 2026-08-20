using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Shared.TestInfrastructure
{
    public interface IMessageProcessingOperation<TState>
    {
        Task<TState> ReadAsync(CancellationToken cancellationToken);

        bool IsCompleted(TState state);

        string Describe(TState state);
    }

    public interface IStartableMessageProcessingOperation<TState>
        : IMessageProcessingOperation<TState>
    {
        Task StartAsync(CancellationToken cancellationToken);
    }
}