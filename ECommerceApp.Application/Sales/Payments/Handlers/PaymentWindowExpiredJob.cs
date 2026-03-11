using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Sales.Payments;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments.Handlers
{
    internal sealed class PaymentWindowExpiredJob : IScheduledTask
    {
        public const string JobTaskName = "PaymentWindowExpiredJob";
        public string TaskName => JobTaskName;

        private readonly IPaymentRepository _paymentRepo;
        private readonly IMessageBroker _broker;

        public PaymentWindowExpiredJob(IPaymentRepository paymentRepo, IMessageBroker broker)
        {
            _paymentRepo = paymentRepo;
            _broker = broker;
        }

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            if (context.EntityId is null || !int.TryParse(context.EntityId, out var paymentId))
            {
                context.ReportFailure($"Invalid EntityId: '{context.EntityId}'.");
                return;
            }

            var payment = await _paymentRepo.GetByIdAsync(paymentId, cancellationToken);
            if (payment is null)
            {
                context.ReportSuccess("No-op: payment not found.");
                return;
            }

            if (payment.Status != PaymentStatus.Pending)
            {
                context.ReportSuccess($"No-op: payment {paymentId} is already '{payment.Status}'.");
                return;
            }

            var @event = payment.Expire();
            await _paymentRepo.UpdateAsync(payment, cancellationToken);

            await _broker.PublishAsync(new PaymentExpired(@event.PaymentId, @event.OrderId, @event.OccurredAt));

            context.ReportSuccess($"Payment {paymentId} expired for order {payment.OrderId.Value}.");
        }
    }
}
