using ECommerceApp.Application.Supporting.Communication.Emails;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Shared.TestInfrastructure
{
    /// <summary>
    /// Test-only <see cref="IEmailService"/> that counts calls instead of sending anything —
    /// for Communication handlers' <c>DuplicateDeliveryTests</c>, which have no DB state to assert
    /// on and instead need to prove the wrapped side effect (the email send) happened exactly once
    /// across a simulated redelivery.
    /// </summary>
    public sealed class CountingEmailService : IEmailService
    {
        private readonly ConcurrentBag<EmailTemplate> _sent = new();

        public int SentCount => _sent.Count;

        public Task SendAsync(EmailTemplate template, CancellationToken ct = default)
        {
            _sent.Add(template);
            return Task.CompletedTask;
        }
    }
}
