using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.TimeManagement
{
    public interface IJobTrigger
    {
        Task TriggerAsync(string jobName, CancellationToken ct = default);
    }
}
