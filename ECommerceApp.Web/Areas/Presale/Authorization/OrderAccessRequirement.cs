using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECommerceApp.Web.Areas.Presale.Authorization
{
    public static class GuestAccessDefaults
    {
        public const string AuthenticationScheme = "GuestAccess";
        public const string CookieName = "ecommerceapp_guest_access";
        public const string OrderIdClaim = "OrderAccessOrderId";
        public const string BackingTokenClaim = "OrderAccessBackingToken";
        public const string ValidatedAtClaim = "OrderAccessValidatedAt";
    }

    public sealed class OrderAccessRequirement : IAuthorizationRequirement
    {
    }

    public sealed record OrderAccessResource(int OrderId, string UserId);

    /// <summary>
    /// The one place <see cref="OrderAccess"/>-policy failures decide what to show the caller. A
    /// wrong-owner <c>Identity.Application</c> user is genuinely forbidden (existing precedent). Anyone
    /// else — no claim at all, or a <c>GuestAccess</c> principal scoped to a different order — is routed
    /// to the unified order-lookup path instead of a dead-end Access Denied page, since they may still be
    /// able to prove ownership of the order they actually asked for via email+code.
    /// </summary>
    public static class OrderAccessDenial
    {
        public static IActionResult Result(Controller controller, ClaimsPrincipal user, int orderId)
        {
            if (user.Identity?.AuthenticationType == IdentityConstants.ApplicationScheme)
                return controller.Forbid();
            return controller.RedirectToAction("Order", "Checkout", new { area = "Presale", id = orderId });
        }
    }
}