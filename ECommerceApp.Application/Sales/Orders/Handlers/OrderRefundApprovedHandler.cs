using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderRefundApprovedHandler : IMessageHandler<RefundApproved>
    {
        private readonly IOrderService _orders;

        public OrderRefundApprovedHandler(IOrderService orders)
        {
            _orders = orders;
        }

        public async Task HandleAsync(RefundApproved message, CancellationToken ct = default)
        {
            await _orders.AddRefundAsync(message.OrderId, message.RefundId, ct);
        }
    }
}
