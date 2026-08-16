using Microsoft.AspNetCore.Http;
using System;
using System.Security.Cryptography;

namespace ECommerceApp.Web.Areas.Presale
{
    internal static class OrderAccessCookie
    {
        public const string CookieName = "ecommerceapp_order_access";

        public static CookieOptions CookieOptions => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(60)
        };

        public static string NewToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(16);
            return $"oat_{Convert.ToHexString(bytes).ToLowerInvariant()}";
        }
    }
}
