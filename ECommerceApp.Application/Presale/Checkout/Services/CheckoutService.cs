using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class CheckoutService : ICheckoutService
    {
        private readonly ISoftReservationService _softReservationService;
        private readonly IOrderClient _orderClient;
        private readonly ICartService _cartService;
        private readonly IOptionsMonitor<PresaleOptions> _options;

        public CheckoutService(
            ISoftReservationService softReservationService,
            IOrderClient orderClient,
            ICartService cartService,
            IOptionsMonitor<PresaleOptions> options)
        {
            _softReservationService = softReservationService;
            _orderClient = orderClient;
            _cartService = cartService;
            _options = options;
        }

        public async Task<InitiateCheckoutResult> InitiateAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            var cart = await _cartService.GetCartAsync(userId, ct);
            if (cart is null || cart.Lines.Count == 0)
            {
                return InitiateCheckoutResult.EmptyCart();
            }

            var existing = await _softReservationService.GetAllForUserAsync(userId, ct);
            if (existing.Any(r => r.Status == SoftReservationStatus.Active))
            {
                return InitiateCheckoutResult.CheckoutAlreadyInProgress();
            }

            var succeeded = new List<int>();
            var unavailable = new List<int>();

            foreach (var line in cart.Lines)
            {
                var held = await _softReservationService.HoldAsync(line.ProductId, userId.Value, line.Quantity, ct);
                if (held)
                {
                    succeeded.Add(line.ProductId);
                }
                else
                {
                    unavailable.Add(line.ProductId);
                }
            }

            if (succeeded.Count == 0)
                return InitiateCheckoutResult.AllUnavailable(unavailable);

            var reservedProductIds = succeeded.Select(id => new PresaleProductId(id)).ToList();
            await _cartService.RemoveRangeAsync(userId, reservedProductIds, ct);

            return InitiateCheckoutResult.Reserved(succeeded.Count, unavailable);
        }

        public async Task<CheckoutResult> PlaceOrderAsync(PresaleUserId userId, int customerId, int currencyId, CheckoutCustomer customer, CancellationToken ct = default)
        {
            var reservations = await _softReservationService.GetAllForUserAsync(userId, ct);
            if (reservations.Count == 0)
                return CheckoutResult.NoReservations();

            var acceptanceWindow = _options.CurrentValue.PlaceOrderAcceptanceWindow;
            var now = DateTime.UtcNow;
            var activeReservations = reservations.Where(r => r.Status == SoftReservationStatus.Active).ToList();
            if (activeReservations.Count == 0 || activeReservations.All(r => r.ExpiresAt.Add(acceptanceWindow) < now))
                return CheckoutResult.Expired();

            await _softReservationService.CommitAllForUserAsync(userId, ct);

            var lines = reservations
                .Select(r => new CheckoutLine(r.ProductId.Value, r.Quantity.Value, r.UnitPrice.Amount))
                .ToList();

            var result = await _orderClient.PlaceOrderAsync(customerId, currencyId, userId.Value, customer, lines, ct);
            if (!result.IsSuccess)
            {
                await _softReservationService.RevertAllForUserAsync(userId, ct);
                return CheckoutResult.Failed(result.FailureReason!);
            }

            return CheckoutResult.Succeeded(result.OrderId!.Value);
        }

        public async Task CancelAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            await _softReservationService.RemoveActiveForUserAsync(userId.Value, ct);
        }

        public async Task<bool> HasActiveCheckoutAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            var existing = await _softReservationService.GetAllForUserAsync(userId, ct);
            return existing.Any(r => r.Status == SoftReservationStatus.Active);
        }

        public async Task<int?> GetSecondsRemainingAsync(PresaleUserId userId, DateTime requestStartedAt, CancellationToken ct = default)
        {
            var existing = await _softReservationService.GetAllForUserAsync(userId, ct);
            var active = existing.Where(r => r.Status == SoftReservationStatus.Active).ToList();
            if (active.Count == 0)
                return null;
            var earliest = active.Min(r => r.ExpiresAt);
            var countdown = ReservationCountdown.From(earliest, requestStartedAt);
            // Return null when expired so the UI treats it as no active checkout.
            // The grace period job will clean up the DB record within the next minute.
            return countdown.IsExpired ? null : countdown.Seconds;
        }
    }
}

