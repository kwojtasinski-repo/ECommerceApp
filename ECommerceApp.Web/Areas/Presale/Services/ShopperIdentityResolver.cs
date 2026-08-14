using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Security.Claims;

namespace ECommerceApp.Web.Areas.Presale.Services
{
    internal sealed class ShopperIdentityResolver : IShopperIdentityResolver
    {
        public PresaleUserId Resolve(HttpContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var claim = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (claim is null || string.IsNullOrWhiteSpace(claim.Value))
                    throw new ArgumentNullException(nameof(claim));

                return new PresaleUserId(claim.Value);
            }

            var existing = context.Request.Cookies[GuestSession.CookieName];
            if (!string.IsNullOrWhiteSpace(existing))
                return new PresaleUserId(existing);

            var token = GuestSession.NewToken();
            context.Response.Cookies.Append(GuestSession.CookieName, token, GuestSession.CookieOptions);
            return new PresaleUserId(token);
        }
    }
}