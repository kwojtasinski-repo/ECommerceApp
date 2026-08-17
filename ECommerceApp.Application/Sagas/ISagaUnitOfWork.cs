using ECommerceApp.Application.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sagas
{
    public interface ISagaUnitOfWork
    {
        Task<IOutboxTransaction> BeginTransactionAsync(CancellationToken ct = default);
    }
}