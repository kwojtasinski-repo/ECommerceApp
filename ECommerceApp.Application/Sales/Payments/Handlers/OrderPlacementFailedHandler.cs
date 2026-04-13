using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Sales.Payments;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments.Handlers
{
    internal sealed class OrderPlacementFailedHandler : IMessageHandler<OrderPlacementFailed>
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IDeferredJobScheduler _scheduler;

        public OrderPlacementFailedHandler(IPaymentRepository paymentRepo, IDeferredJobScheduler scheduler)
        {
            _paymentRepo = paymentRepo;
            _scheduler = scheduler;
        }

        public async Task HandleAsync(OrderPlacementFailed message, CancellationToken ct = default)
        {
            var payment = await _paymentRepo.GetByOrderIdAsync(message.OrderId, ct);
            if (payment is null)
            {
                return;
            }

            payment.Cancel();
            await _paymentRepo.UpdateAsync(payment, ct);
            await _scheduler.CancelAsync(
                PaymentWindowExpiredJob.JobTaskName,
                payment.Id.Value.ToString(),
                ct);
        }
    }
}
