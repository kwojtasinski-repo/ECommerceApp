using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Sales.Orders;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderPaymentExpiredHandler : IMessageHandler<PaymentExpired>
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IMessageBroker _broker;

        public OrderPaymentExpiredHandler(IOrderRepository orderRepo, IMessageBroker broker)
        {
            _orderRepo = orderRepo;
            _broker = broker;
        }

        public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(message.OrderId, ct);
            if (order is null)
                return;

            if (order.IsCancelled || order.IsPaid)
                return;

            var items = order.OrderItems
                .Select(i => new OrderCancelledItem(i.ItemId.Value, i.Quantity))
                .ToList();

            order.Cancel();
            await _orderRepo.UpdateAsync(order, ct);

            await _broker.PublishAsync(new OrderCancelled(message.OrderId, items, DateTime.UtcNow));
        }
    }
}
