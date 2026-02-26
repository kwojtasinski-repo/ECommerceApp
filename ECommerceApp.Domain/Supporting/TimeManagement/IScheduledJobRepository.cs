using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Supporting.TimeManagement
{
    public interface IScheduledJobRepository
    {
        Task<ScheduledJob?> GetByNameAsync(string name, CancellationToken ct = default);
        Task<IReadOnlyList<ScheduledJob>> GetAllAsync(CancellationToken ct = default);
        Task AddAsync(ScheduledJob job, CancellationToken ct = default);
        Task UpdateAsync(ScheduledJob job, CancellationToken ct = default);
    }
}
