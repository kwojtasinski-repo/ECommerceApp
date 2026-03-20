using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderRefundRejectedHandler : IMessageHandler<RefundRejected>
    {
        private readonly IOrderService _orders;

        public OrderRefundRejectedHandler(IOrderService orders)
        {
            _orders = orders;
        }

        public async Task HandleAsync(RefundRejected message, CancellationToken ct = default)
        {
            await _orders.RemoveRefundByRefundIdAsync(message.RefundId, ct);
        }
    }
}
