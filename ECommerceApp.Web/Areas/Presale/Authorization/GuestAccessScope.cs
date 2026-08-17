using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace ECommerceApp.Web.Areas.Presale.Authorization
{
    /// <summary>
    /// A <c>GuestAccess</c>-authenticated principal is scoped to exactly one order (the
    /// <see cref="GuestAccessDefaults.OrderIdClaim"/> claim), by design — see
    /// <see cref="OrderAccessAuthorizationHandler"/> for the resource-level enforcement of that scope
    /// on the id-addressed order/payment/refund routes. This is the same scope applied to the
    /// per-caller "My..." list endpoints (<c>MyOrders</c>/<c>MyPayments</c>/<c>MyRefunds</c>), which
    /// filter by <see cref="ClaimTypes.NameIdentifier"/> alone and would otherwise surface every order
    /// tied to that identifier — including an earlier order from the same guest session, once
    /// <c>ShopperIdentityResolver</c> starts resolving the caller's id from the active GuestAccess
    /// ticket instead of minting a fresh one.
    /// </summary>
    public static class GuestAccessScope
    {
        public static int? GetScopedOrderId(ClaimsPrincipal user)
        {
            if (user.Identity?.AuthenticationType != GuestAccessDefaults.AuthenticationScheme)
                return null;

            var claim = user.FindFirstValue(GuestAccessDefaults.OrderIdClaim);
            return int.TryParse(claim, out var orderId) ? orderId : null;
        }

        /// <summary>
        /// The one call site every "My..." caller-scoped list (<c>MyOrders</c>/<c>MyPayments</c>/
        /// <c>MyRefunds</c>, and any future one) should go through instead of re-deriving the
        /// GuestAccess-vs-real-account branch itself. A real registered caller (not GuestAccess) gets
        /// <paramref name="items"/> back unchanged; a GuestAccess caller gets it narrowed to the single
        /// order their ticket is scoped to.
        /// </summary>
        public static IReadOnlyList<T> ScopeToCurrentOrder<T>(
            ClaimsPrincipal user,
            IReadOnlyList<T> items,
            Func<T, int> orderIdSelector)
        {
            var scopedOrderId = GetScopedOrderId(user);
            if (!scopedOrderId.HasValue)
                return items;

            return items.Where(item => orderIdSelector(item) == scopedOrderId.Value).ToList();
        }
    }
}
