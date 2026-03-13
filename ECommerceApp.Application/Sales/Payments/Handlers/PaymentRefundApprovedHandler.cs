using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Payments.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments.Handlers
{
    internal sealed class PaymentRefundApprovedHandler : IMessageHandler<RefundApproved>
    {
        private readonly IPaymentService _payments;

        public PaymentRefundApprovedHandler(IPaymentService payments)
        {
            _payments = payments;
        }

        public async Task HandleAsync(RefundApproved message, CancellationToken ct = default)
        {
            await _payments.ProcessRefundAsync(message.OrderId, message.RefundId, ct);
        }
    }
}
