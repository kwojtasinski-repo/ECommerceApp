using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Sales.Orders;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderPaymentExpiredHandler : IMessageHandler<PaymentExpired>
    {
        private readonly IOrderRepository _orderRepo;

        public OrderPaymentExpiredHandler(IOrderRepository orderRepo)
        {
            _orderRepo = orderRepo;
        }

        public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
        {
            var order = await _orderRepo.GetByIdWithItemsAsync(message.OrderId, ct);
            if (order is null)
                return;

            if (order.Status != OrderStatus.Placed)
                return;

            order.ExpirePayment();
            await _orderRepo.UpdateAsync(order, ct);
        }
    }
}
