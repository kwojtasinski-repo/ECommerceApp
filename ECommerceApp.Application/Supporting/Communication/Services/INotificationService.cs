using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Services
{
    public interface INotificationService
    {
        Task NotifyAsync(string userId, string eventType, string message, CancellationToken ct = default);
    }
}
