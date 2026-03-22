using ECommerceApp.API.Options;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Results;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.API.Controllers.V2
{
    [Authorize]
    [Route("api/v2/checkout")]
    public sealed class CheckoutController : BaseController
    {
        private readonly ICheckoutService _checkout;
        private readonly ISoftReservationService _softReservations;
        private readonly IOptions<WebOptions> _webOptions;

        public CheckoutController(ICheckoutService checkout, ISoftReservationService softReservations, IOptions<WebOptions> webOptions)
        {
            _checkout = checkout;
            _softReservations = softReservations;
            _webOptions = webOptions;
        }

        // POST api/v2/checkout/initiate
        // Locks prices for all cart items into SoftReservations with a 15-minute TTL.
        // Must be called before price-changes and confirm. Re-calling refreshes prices and TTL.
        [HttpPost("initiate")]
        [Authorize(Policy = "TrustedApiUser")]
        public async Task<IActionResult> Initiate(CancellationToken ct = default)
        {
            var userId = new PresaleUserId(GetUserId());
            var result = await _checkout.InitiateAsync(userId, ct);

            return result switch
            {
                InitiateCheckoutResult.Completed c => Ok(new { c.ReservedCount, c.UnavailableProductIds }),
                InitiateCheckoutResult.NothingReserved n => Conflict(new { Error = "All items are currently unavailable.", n.UnavailableProductIds }),
                InitiateCheckoutResult.CartEmpty => BadRequest(new { Error = "Cart is empty." }),
                InitiateCheckoutResult.AlreadyInProgress => Conflict(new { Error = "A checkout is already in progress." }),
                _ => StatusCode(500)
            };
        }

        // GET api/v2/checkout/price-changes
        // Returns lines where the locked reservation price differs from the current catalog price.
        // Empty list = prices unchanged since checkout initiation — safe to confirm.
        [HttpGet("price-changes")]
        public async Task<IActionResult> GetPriceChanges(CancellationToken ct = default)
        {
            var userId = new PresaleUserId(GetUserId());
            var changes = await _softReservations.GetPriceChangesAsync(userId, ct);
            return Ok(changes);
        }

        // POST api/v2/checkout/confirm
        // Places the order using inline customer data from the form. The customer data
        // (name, address, contact) is submitted directly — the caller pre-fills it from
        // AccountProfile or manual entry. No server-side customer lookup is performed.
        [HttpPost("confirm")]
        [Authorize(Policy = "TrustedApiUser")]
        public async Task<IActionResult> Confirm([FromBody] ConfirmCheckoutRequest request, CancellationToken ct = default)
        {
            var userId = new PresaleUserId(GetUserId());
            var result = await _checkout.PlaceOrderAsync(userId, request.CustomerId, request.CurrencyId, request.Customer, ct);

            return result switch
            {
                CheckoutResult.Success s => Ok(new
                {
                    s.OrderId,
                    paymentUrl = $"{_webOptions.Value.BaseUrl}/Sales/Payments/Create?orderId={s.OrderId}"
                }),
                CheckoutResult.NoSoftReservations => BadRequest(new { Error = "Checkout not initiated. Please initiate checkout first." }),
                CheckoutResult.StockUnavailable u => Conflict(new { Error = $"Product {u.ProductId} is no longer available in the requested quantity." }),
                CheckoutResult.OrderFailed f => UnprocessableEntity(new { Error = f.Reason }),
                _ => StatusCode(500)
            };
        }
    }

    public sealed record ConfirmCheckoutRequest(int CustomerId, int CurrencyId, CheckoutCustomer Customer);
}

