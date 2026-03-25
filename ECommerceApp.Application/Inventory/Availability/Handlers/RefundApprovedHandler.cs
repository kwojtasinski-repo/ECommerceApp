using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
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
            foreach (var item in message.Items)
            {
                await _stockService.ReturnAsync(item.ProductId, item.Quantity, ct);
            }
        }
    }
}
