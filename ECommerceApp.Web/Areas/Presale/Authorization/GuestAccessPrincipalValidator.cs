using ECommerceApp.Application.Presale.Checkout.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ECommerceApp.Web.Areas.Presale.Authorization
{
    /// <summary>
    /// GuestAccess cookie revocation check, run on every authenticated request via
    /// <c>CookieAuthenticationOptions.Events.OnValidatePrincipal</c> — re-checks the backing
    /// <c>OrderAccessToken</c> row still exists, capped at once per <see cref="RevalidationInterval"/>
    /// per principal (cached via the <see cref="GuestAccessDefaults.ValidatedAtClaim"/> claim baked into
    /// the ticket) so a deleted/revoked token stops granting access without a per-request DB hit.
    /// Extracted out of <c>Startup.cs</c>'s options-configuration lambda so the logic itself is directly
    /// unit-testable — <c>Startup.cs</c> only resolves an instance per request via
    /// <c>HttpContext.RequestServices</c>, which is a required ASP.NET Core constraint for cookie events
    /// (the <c>AddCookie</c> options delegate runs at <c>ConfigureServices</c> time, before any
    /// request-scoped service exists to constructor-inject), not a service-locator choice in this class.
    /// </summary>
    public sealed class GuestAccessPrincipalValidator
    {
        public static readonly TimeSpan RevalidationInterval = TimeSpan.FromMinutes(5);

        private readonly IOrderAccessService _orderAccessService;

        public GuestAccessPrincipalValidator(IOrderAccessService orderAccessService)
        {
            _orderAccessService = orderAccessService;
        }

        public async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            var validatedAt = context.Principal?.FindFirst(GuestAccessDefaults.ValidatedAtClaim)?.Value;
            if (DateTimeOffset.TryParse(validatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastValidated)
                && DateTimeOffset.UtcNow - lastValidated < RevalidationInterval)
                return;

            var orderIdValue = context.Principal?.FindFirst(GuestAccessDefaults.OrderIdClaim)?.Value;
            var token = context.Principal?.FindFirst(GuestAccessDefaults.BackingTokenClaim)?.Value;
            if (!int.TryParse(orderIdValue, out var orderId) || string.IsNullOrWhiteSpace(token))
            {
                context.RejectPrincipal();
                return;
            }

            if (!await _orderAccessService.HasAccessAsync(orderId, token, context.HttpContext.RequestAborted))
            {
                context.RejectPrincipal();
                return;
            }

            var identity = context.Principal.Identity as ClaimsIdentity;
            var existingValidationClaim = identity?.FindFirst(GuestAccessDefaults.ValidatedAtClaim);
            if (existingValidationClaim is not null)
                identity.RemoveClaim(existingValidationClaim);
            identity?.AddClaim(new Claim(
                GuestAccessDefaults.ValidatedAtClaim,
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            context.ShouldRenew = true;
        }
    }
}
