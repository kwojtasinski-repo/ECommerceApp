using Microsoft.AspNetCore.Http;
using System;
using System.Security.Cryptography;

namespace ECommerceApp.Web.Areas.Presale
{
    internal static class GuestSession
    {
        public const string CookieName = "ecommerceapp_guest";

        public static CookieOptions CookieOptions => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddMinutes(16)
        };

        public static string NewToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(16);
            return $"gst_{Convert.ToHexString(bytes).ToLowerInvariant()}";
        }
    }
}