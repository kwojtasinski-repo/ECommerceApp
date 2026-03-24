using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Sales.Payments;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Sales.Payments.Handlers
{
    internal sealed class OrderPlacedHandler : IMessageHandler<OrderPlaced>
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IDeferredJobScheduler _scheduler;

        public OrderPlacedHandler(IPaymentRepository paymentRepo, IDeferredJobScheduler scheduler)
        {
            _paymentRepo = paymentRepo;
            _scheduler = scheduler;
        }

        public async Task HandleAsync(OrderPlaced message, CancellationToken ct = default)
        {
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
        }
    }
}
