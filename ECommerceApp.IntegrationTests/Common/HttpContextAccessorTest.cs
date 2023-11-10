using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ECommerceApp.IntegrationTests.Common
{
    internal sealed class HttpContextAccessorTest : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; } = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal()
        };
    }
}
