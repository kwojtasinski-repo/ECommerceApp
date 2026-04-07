using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Emails
{
    /// <summary>
    /// Port for transactional email delivery.
    /// The Communication BC handlers build <see cref="EmailTemplate"/> instances
    /// and call this service — they never compose raw HTML or know the delivery provider.
    /// </summary>
    public interface IEmailService
    {
        Task SendAsync(EmailTemplate template, CancellationToken ct = default);
    }
}
