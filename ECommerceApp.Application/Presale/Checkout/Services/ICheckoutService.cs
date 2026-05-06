using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Domain.Presale.Checkout;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    public interface ICheckoutService
    {
        Task<InitiateCheckoutResult> InitiateAsync(PresaleUserId userId, CancellationToken ct = default);
        Task<CheckoutResult> PlaceOrderAsync(PresaleUserId userId, int customerId, int currencyId, CheckoutCustomer customer, CancellationToken ct = default);
        Task CancelAsync(PresaleUserId userId, CancellationToken ct = default);
        Task<bool> HasActiveCheckoutAsync(PresaleUserId userId, CancellationToken ct = default);
        /// <summary>
        /// Returns seconds remaining for the active checkout measured from <paramref name="requestStartedAt"/>,
        /// or null when there is no active checkout.
        /// </summary>
        Task<int?> GetSecondsRemainingAsync(PresaleUserId userId, DateTime requestStartedAt, CancellationToken ct = default);
    }
}
