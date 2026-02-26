using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.TimeManagement
{
    public interface IScheduledTask
    {
        string TaskName { get; }
        Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken);
    }
}
