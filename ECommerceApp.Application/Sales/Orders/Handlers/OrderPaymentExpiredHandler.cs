using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Domain.Sales.Orders;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Orders.Handlers
{
    internal sealed class OrderPaymentExpiredHandler : IMessageHandler<PaymentExpired>
    {
        private readonly IOrderRepository _orderRepo;
        private readonly ILogger<OrderPaymentExpiredHandler> _logger;

        public OrderPaymentExpiredHandler(
            IOrderRepository orderRepo,
            ILogger<OrderPaymentExpiredHandler> logger)
        {
            _orderRepo = orderRepo;
            _logger = logger;
        }

        public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "[Orders][OrderPaymentExpiredHandler] Received PaymentExpired. PaymentId={PaymentId} OrderId={OrderId} CorrelationId={CorrelationId}",
                message.PaymentId, message.OrderId, message.CorrelationId);

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
