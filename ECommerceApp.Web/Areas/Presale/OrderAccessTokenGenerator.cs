using System;
using System.Security.Cryptography;

namespace ECommerceApp.Web.Areas.Presale
{
    /// <summary>
    /// Generates the opaque backing token persisted by <c>IOrderAccessService</c>/<c>OrderAccessToken</c>
    /// and referenced by the <c>GuestAccess</c> cookie's <see cref="Authorization.GuestAccessDefaults.BackingTokenClaim"/>
    /// claim (Phase 9's <c>OnValidatePrincipal</c> revocation check). The token used to also back a
    /// separate, directly-read <c>ecommerceapp_order_access</c> cookie (Phase 7/8) — that per-request-cookie
    /// mechanism was fully superseded once every consuming action migrated to the GuestAccess scheme +
    /// OrderAccess policy (Phase 9 plan Step 9), so only the token generator itself remains.
    /// </summary>
    internal static class OrderAccessTokenGenerator
    {
        public static string NewToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(16);
            return $"oat_{Convert.ToHexString(bytes).ToLowerInvariant()}";
        }
    }
}
