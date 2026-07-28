using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Sales.Payments;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Sales.Payments;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments.Handlers
{
    internal sealed class PaymentWindowExpiredJob : IScheduledTask
    {
        public const string JobTaskName = "PaymentWindowExpiredJob";
        public string TaskName => JobTaskName;

        private readonly IPaymentRepository _paymentRepo;
        private readonly IPaymentsUnitOfWork _unitOfWork;
        private readonly IOutboxWriter _outboxWriter;
        private readonly ILogger<PaymentWindowExpiredJob> _logger;

        public PaymentWindowExpiredJob(
            IPaymentRepository paymentRepo,
            IPaymentsUnitOfWork unitOfWork,
            IOutboxWriter outboxWriter,
            ILogger<PaymentWindowExpiredJob> logger)
        {
            _paymentRepo = paymentRepo;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
            _logger = logger;
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

            var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await using (transaction)
            {
                await _paymentRepo.UpdateAsync(payment, cancellationToken);

                var correlationId = Guid.NewGuid();
                _logger.LogInformation(
                    "[Payments][PaymentWindowExpiredJob] Publishing PaymentExpired. PaymentId={PaymentId} OrderId={OrderId} CorrelationId={CorrelationId}",
                    paymentId, payment.OrderId.Value, correlationId);

                await _outboxWriter.EnqueueAsync(
                    new PaymentExpired(@event.PaymentId, @event.OrderId, @event.OccurredAt, correlationId),
                    transaction,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            context.ReportSuccess($"Payment {paymentId} expired for order {payment.OrderId.Value}.");
        }
    }
}
