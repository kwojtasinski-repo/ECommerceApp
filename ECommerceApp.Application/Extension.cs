using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;

[assembly: InternalsVisibleTo("ECommerceApp.UnitTests")]
[assembly: InternalsVisibleTo("ECommerceApp.IntegrationTests")]
namespace ECommerceApp.Application
{
    public static class Extension
    {
        public static string GetUserId(this IHttpContextAccessor httpContextAccessor)
        {
            return httpContextAccessor.HttpContext?.User?.GetUserId();
        }

        public static string GetUserId(this ClaimsPrincipal claimsPrincipal)
        {
            return claimsPrincipal.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        }

        public static string GetUserRole(this IHttpContextAccessor httpContextAccessor)
        {
            return httpContextAccessor.HttpContext?.User?.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        }
    }
}
