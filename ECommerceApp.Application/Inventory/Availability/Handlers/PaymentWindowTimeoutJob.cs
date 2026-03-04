using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Inventory.Availability;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Inventory.Availability.Handlers
{
    internal sealed class PaymentWindowTimeoutJob : IScheduledTask
    {
        public const string JobTaskName = "PaymentWindowTimeoutJob";

        public string TaskName => JobTaskName;

        private readonly IStockItemRepository _stockItemRepo;
        private readonly IReservationRepository _reservationRepo;

        public PaymentWindowTimeoutJob(
            IStockItemRepository stockItemRepo,
            IReservationRepository reservationRepo)
        {
            _stockItemRepo = stockItemRepo;
            _reservationRepo = reservationRepo;
        }

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            if (context.EntityId is null)
            {
                context.ReportFailure("Missing EntityId.");
                return;
            }

            var parts = context.EntityId.Split(':');
            if (parts.Length != 3
                || !int.TryParse(parts[0], out var orderId)
                || !int.TryParse(parts[1], out var productId)
                || !int.TryParse(parts[2], out var quantity))
            {
                context.ReportFailure($"Invalid EntityId format: '{context.EntityId}'. Expected '{{orderId}}:{{productId}}:{{quantity}}'.");
                return;
            }

            var reservation = await _reservationRepo.GetByOrderAndProductAsync(orderId, productId, cancellationToken);

            if (reservation is null || reservation.Status == ReservationStatus.Confirmed)
            {
                context.ReportSuccess("No-op: already paid or reservation not found.");
                return;
            }

            var stock = await _stockItemRepo.GetByProductIdAsync(productId, cancellationToken);
            if (stock != null)
            {
                stock.Release(quantity);
                await _stockItemRepo.UpdateAsync(stock, cancellationToken);
            }

            await _reservationRepo.DeleteAsync(reservation, cancellationToken);

            context.ReportSuccess($"Reservation released for order {orderId}, product {productId}.");
        }
    }
}
