using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Domain.Presale.Checkout;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    public interface ICheckoutService
    {
        Task<InitiateCheckoutResult> InitiateAsync(PresaleUserId userId, CancellationToken ct = default);
        Task<CheckoutResult> PlaceOrderAsync(PresaleUserId userId, int customerId, int currencyId, CheckoutCustomer customer, CancellationToken ct = default);
    }
}
