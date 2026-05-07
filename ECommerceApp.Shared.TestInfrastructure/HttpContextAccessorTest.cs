using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;

namespace ECommerceApp.Shared.TestInfrastructure
{
    public sealed class HttpContextAccessorTest : IHttpContextAccessor
    {
        public HttpContextAccessorTest(IServiceProvider services)
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(),
                RequestServices = services
            };
        }

        public HttpContext HttpContext { get; set; }
    }
}

