using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class RefundApprovedHandler : IMessageHandler<RefundApproved>
    {
        private readonly IStockService _stockService;

        public RefundApprovedHandler(IStockService stockService)
        {
            _stockService = stockService;
        }

        public async Task HandleAsync(RefundApproved message, CancellationToken ct = default)
        {
            await _stockService.ReturnAsync(message.ProductId, message.Quantity, ct);
        }
    }
}
