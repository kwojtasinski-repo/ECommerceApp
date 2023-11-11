using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace ECommerceApp.IntegrationTests.Common
{
    public class BaseTest<T> : CustomWebApplicationFactory<Startup>, IDisposable where T : class
    {
        protected readonly string PROPER_CUSTOMER_ID = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";
        protected readonly T _service;

        protected BaseTest()
        {
            _service = Services.GetService(typeof(T)) as T;
        }

        public new virtual void Dispose()
        {
            var context = Services.GetService(typeof(Context)) as Context;
            context.Database.EnsureDeleted();
            context.Dispose();
            base.Dispose();
        }

        protected void SetHttpContextUserId(string userId)
        {
            var httpContextAccessor = Services.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
            httpContextAccessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }));
        }
    }
}
