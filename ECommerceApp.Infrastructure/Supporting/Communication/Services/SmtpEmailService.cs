using ECommerceApp.Application.Supporting.Communication.Emails;
using ECommerceApp.Infrastructure.Supporting.Communication.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.Communication.Services
{
    /// <summary>
    /// Skeleton SMTP email service. Logs the full email intent —
    /// no actual connection is made until a provider (MailKit, SendGrid, etc.)
    /// is wired in place of this class.
    /// </summary>
    internal sealed class SmtpEmailService : IEmailService
    {
        private readonly ILogger<SmtpEmailService> _logger;
        private readonly SmtpEmailOptions _options;

        public SmtpEmailService(IOptions<SmtpEmailOptions> options, ILogger<SmtpEmailService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public Task SendAsync(EmailTemplate template, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "[Email] Would send via {Host}:{Port} | From: {FromAddress} | To: {To} | " +
                "Subject: {Subject} | Body length: {BodyLength} chars | Actions: {ActionCount}",
                _options.Host,
                _options.Port,
                _options.FromAddress,
                template.To,
                template.Subject,
                template.Body.Length,
                template.Actions?.Count ?? 0);
            return Task.CompletedTask;
        }
    }
}
