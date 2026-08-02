using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Sales.Payments;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments.Handlers
{
    internal sealed class OrderPlacementFailedHandler : IIdAwareMessageHandler<OrderPlacementFailed>
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IDeferredJobScheduler _scheduler;
        private readonly IPaymentsUnitOfWork _unitOfWork;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderPlacementFailedHandler(
            IPaymentRepository paymentRepo,
            IDeferredJobScheduler scheduler,
            IPaymentsUnitOfWork unitOfWork,
            IProcessedMessageGuard processedMessageGuard)
        {
            _paymentRepo = paymentRepo;
            _scheduler = scheduler;
            _unitOfWork = unitOfWork;
            _processedMessageGuard = processedMessageGuard;
        }

        public async Task HandleAsync(OrderPlacementFailed message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(OrderPlacementFailed message, long outboxMessageId, CancellationToken ct = default)
        {
            var transaction = await _unitOfWork.BeginTransactionAsync(ct);
            await using (transaction)
            {
                var handlerType = GetType().FullName
                    ?? throw new InvalidOperationException("Handler type name is unavailable.");
                if (!await _processedMessageGuard.TryMarkProcessedAsync(
                        outboxMessageId, handlerType, transaction, ct))
                {
                    return;
                }

                var payment = await _paymentRepo.GetByOrderIdAsync(message.OrderId, ct);
                if (payment is null)
                {
                    await transaction.CommitAsync(ct);
                    return;
                }

                payment.Cancel();
                await _paymentRepo.UpdateAsync(payment, ct);
                await _scheduler.CancelAsync(
                    PaymentWindowExpiredJob.JobTaskName,
                    payment.Id.Value.ToString(),
                    ct);
                await transaction.CommitAsync(ct);
            }
        }
    }
}
