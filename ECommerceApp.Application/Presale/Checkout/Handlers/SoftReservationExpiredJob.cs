using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Handlers
{
    internal sealed class SoftReservationExpiredJob : IScheduledTask
    {
        public const string JobTaskName = "SoftReservationExpiredJob";

        public string TaskName => JobTaskName;

        private readonly ISoftReservationRepository _reservationRepo;
        private readonly IMemoryCache _cache;

        public SoftReservationExpiredJob(ISoftReservationRepository reservationRepo, IMemoryCache cache)
        {
            _reservationRepo = reservationRepo;
            _cache = cache;
        }

        public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            if (context.EntityId is null)
            {
                context.ReportFailure("Missing EntityId.");
                return;
            }

            if (!int.TryParse(context.EntityId, out var reservationId))
            {
                context.ReportFailure($"Invalid EntityId: '{context.EntityId}'. Expected integer reservation ID.");
                return;
            }

            var reservation = await _reservationRepo.GetByIdAsync(new SoftReservationId(reservationId), cancellationToken);
            if (reservation is null)
            {
                context.ReportSuccess("No-op: reservation already removed.");
                return;
            }

            await _reservationRepo.DeleteAsync(reservation, cancellationToken);
            _cache.Remove(CacheKey(reservation.ProductId.Value, reservation.UserId.Value));

            context.ReportSuccess($"SoftReservation {reservationId} expired and removed.");
        }

        private static string CacheKey(int productId, string userId) => $"sr:{productId}:{userId}";
    }
}
