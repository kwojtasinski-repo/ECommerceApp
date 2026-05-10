using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class PaymentExpiredHandler : IMessageHandler<PaymentExpired>
    {
        private readonly IStockService _stockService;
        private readonly ILogger<PaymentExpiredHandler> _logger;

        public PaymentExpiredHandler(
            IStockService stockService,
            ILogger<PaymentExpiredHandler> logger)
        {
            _stockService = stockService;
            _logger = logger;
        }

        public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "[Inventory][PaymentExpiredHandler] Received PaymentExpired. PaymentId={PaymentId} OrderId={OrderId} CorrelationId={CorrelationId}",
                message.PaymentId, message.OrderId, message.CorrelationId);

            await _stockService.ReleaseAllHoldsForOrderAsync(message.OrderId, ct);
        }
    }
}
