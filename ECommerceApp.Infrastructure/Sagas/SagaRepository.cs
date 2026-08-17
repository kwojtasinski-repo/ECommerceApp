using ECommerceApp.Application.Sagas;
using ECommerceApp.Domain.Sagas;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sagas
{
    internal sealed class SagaRepository : ISagaRepository
    {
        private readonly SagasDbContext _context;

        public SagaRepository(SagasDbContext context)
        {
            _context = context;
        }

        public Task<SagaInstance?> FindRunningAsync(
            string sagaType,
            string correlationId,
            CancellationToken ct = default)
        {
            return _context.Sagas
                .SingleOrDefaultAsync(
                    saga => saga.SagaType == sagaType
                        && saga.CorrelationId == correlationId
                        && saga.Status != SagaInstanceStatus.Completed
                        && saga.Status != SagaInstanceStatus.Failed,
                    ct);
        }

        public async Task AddAsync(SagaInstance instance, CancellationToken ct = default)
        {
            _context.Sagas.Add(instance);
            await _context.SaveChangesAsync(ct);
        }

        public Task<SagaStep?> FindStepAsync(
            long sagaInstanceId,
            string stepName,
            CancellationToken ct = default)
        {
            return _context.Steps.SingleOrDefaultAsync(
                step => step.SagaInstanceId == sagaInstanceId && step.StepName == stepName,
                ct);
        }

        public async Task AddStepAsync(SagaStep step, CancellationToken ct = default)
        {
            _context.Steps.Add(step);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(SagaInstance instance, CancellationToken ct = default)
        {
            _context.Sagas.Update(instance);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateStepAsync(SagaStep step, CancellationToken ct = default)
        {
            _context.Steps.Update(step);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<SagaStep>> GetStepsAsync(
            long sagaInstanceId,
            CancellationToken ct = default)
        {
            return await _context.Steps
                .Where(step => step.SagaInstanceId == sagaInstanceId)
                .AsNoTracking()
                .ToListAsync(ct);
        }
    }
}