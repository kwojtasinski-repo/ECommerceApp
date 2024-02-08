using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Security.Claims;

namespace ECommerceApp.UnitTests.Common
{
    internal sealed class HttpContextAccessorTest : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; } = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal()
        };

        public void SetUserId(string userId)
        {
            HttpContext.User = AddClaimToUser(HttpContext.User, new(ClaimTypes.NameIdentifier, userId));
        }

        public void SetUserRole(string userRole)
        {
            HttpContext.User = AddClaimToUser(HttpContext.User, new Claim(ClaimTypes.Role, userRole));
        }

        private static ClaimsPrincipal AddClaimToUser(ClaimsPrincipal user, Claim claim)
        {
            if (user is null)
            {
                return new ClaimsPrincipal(new List<ClaimsIdentity>()
                    {
                        new (new List<Claim>
                        {
                            new (claim.Type, claim.Value)
                        })
                    });
            }

            var claims = new List<Claim>
            {
                new (claim.Type, claim.Value)
            };
            claims.AddRange(user.Claims);
            return new ClaimsPrincipal(new List<ClaimsIdentity>()
            {
                new (claims)
            });
        }
    }
}
