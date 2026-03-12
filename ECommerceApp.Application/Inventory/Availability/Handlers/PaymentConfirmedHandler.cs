using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class PaymentConfirmedHandler : IMessageHandler<PaymentConfirmed>
    {
        private readonly IStockService _stockService;

        public PaymentConfirmedHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task HandleAsync(PaymentConfirmed message, CancellationToken ct = default)
        {
            await _stockService.ConfirmReservationsByOrderAsync(message.OrderId, ct);
        }
    }
}
