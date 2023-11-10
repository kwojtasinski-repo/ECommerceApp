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
            HttpContext.User = new ClaimsPrincipal(new List<ClaimsIdentity>()
            {
                new ClaimsIdentity(new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                })
            });
        }
    }
}
