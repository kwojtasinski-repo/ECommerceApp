using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Emails
{
    internal sealed class LoggingEmailService : IEmailService
    {
        private readonly ILogger<LoggingEmailService> _logger;

        public LoggingEmailService(ILogger<LoggingEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(EmailTemplate template, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "[Email] To: {ToEmail} \u2014 Subject: {Subject} | Actions: {ActionCount}",
                template.To,
                template.Subject,
                template.Actions?.Count ?? 0);
            return Task.CompletedTask;
        }
    }
}
