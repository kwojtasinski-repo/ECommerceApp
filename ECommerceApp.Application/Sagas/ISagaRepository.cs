using ECommerceApp.Domain.Sagas;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sagas
{
    public interface ISagaRepository
    {
        Task<SagaInstance?> FindRunningAsync(string sagaType, string correlationId, CancellationToken ct = default);
        Task AddAsync(SagaInstance instance, CancellationToken ct = default);
        Task<SagaStep?> FindStepAsync(long sagaInstanceId, string stepName, CancellationToken ct = default);
        Task AddStepAsync(SagaStep step, CancellationToken ct = default);
        Task UpdateAsync(SagaInstance instance, CancellationToken ct = default);
        Task UpdateStepAsync(SagaStep step, CancellationToken ct = default);
        Task<IReadOnlyList<SagaStep>> GetStepsAsync(long sagaInstanceId, CancellationToken ct = default);
    }
}