using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Sales.Payments;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments.Handlers
{
    internal sealed class OrderPlacedHandler : IIdAwareMessageHandler<OrderPlaced>
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IDeferredJobScheduler _scheduler;
        private readonly IPaymentsUnitOfWork _unitOfWork;
        private readonly IProcessedMessageGuard _processedMessageGuard;

        public OrderPlacedHandler(
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

        public async Task HandleAsync(OrderPlaced message, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "This handler requires outboxMessageId; call the IIdAwareMessageHandler overload.");
        }

        public async Task HandleAsync(OrderPlaced message, long outboxMessageId, CancellationToken ct = default)
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

                var payment = Payment.Create(
                    new PaymentOrderId(message.OrderId),
                    message.TotalAmount,
                    message.CurrencyId,
                    message.ExpiresAt,
                    message.UserId);

                await _paymentRepo.AddAsync(payment, ct);

                await _scheduler.ScheduleAsync(
                    PaymentWindowExpiredJob.JobTaskName,
                    payment.Id?.Value.ToString() ?? "0",
                    message.ExpiresAt,
                    ct);
                await transaction.CommitAsync(ct);
            }
        }
    }
}
