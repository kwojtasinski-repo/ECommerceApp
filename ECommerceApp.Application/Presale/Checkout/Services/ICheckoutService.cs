using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Domain.Presale.Checkout;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    public interface ICheckoutService
    {
        Task<CheckoutResult> PlaceOrderAsync(PresaleUserId userId, int customerId, int currencyId, CancellationToken ct = default);
    }
}
