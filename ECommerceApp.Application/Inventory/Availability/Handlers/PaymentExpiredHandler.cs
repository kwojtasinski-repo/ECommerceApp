using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class PaymentExpiredHandler : IMessageHandler<PaymentExpired>
    {
        private readonly IStockService _stockService;

        public PaymentExpiredHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
        {
            await _stockService.ReleaseAllHoldsForOrderAsync(message.OrderId, ct);
        }
    }
}
