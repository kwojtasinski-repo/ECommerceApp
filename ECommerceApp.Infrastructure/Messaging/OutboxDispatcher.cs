using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Constants;
using ECommerceApp.Domain.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Messaging
{
    internal sealed class OutboxDispatcher
    {
        private readonly IModuleClient _moduleClient;
        private readonly IOutboxRepository _outboxRepository;
        private readonly ILogger<OutboxDispatcher> _logger;
        private readonly IOptions<RetryPolicyOptions> _options;

        public OutboxDispatcher(
            IModuleClient moduleClient,
            IOutboxRepository outboxRepository,
            IOptions<RetryPolicyOptions> options,
            ILogger<OutboxDispatcher> logger)
        {
            _moduleClient = moduleClient;
            _outboxRepository = outboxRepository;
            _options = options;
            _logger = logger;
        }

        public async Task DispatchAsync(OutboxMessage message, CancellationToken ct)
        {
            try
            {
                var type = MessageTypeRegistry.TypeFor(message.MessageTypeKey);
                var deserialized = JsonSerializer.Deserialize(message.Payload, type);
                var imessage = (IMessage)deserialized!;

                await _moduleClient.PublishAsync(imessage, message.Id);

                message.MarkDispatched(DateTime.UtcNow);
                await _outboxRepository.UpdateAsync(message, ct);
                _logger.LogInformation("Outbox message {Id} dispatched", message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch outbox message {Id} (type={TypeKey}): {Message}", message.Id, message.MessageTypeKey, ex.Message);
                message.Fail(ex.Message, DateTime.UtcNow, _options?.Value.MaxBackoff);
                await _outboxRepository.UpdateAsync(message, ct);
            }
        }
    }
}
