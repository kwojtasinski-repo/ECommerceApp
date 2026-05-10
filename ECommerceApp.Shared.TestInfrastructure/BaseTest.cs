using ECommerceApp.Infrastructure.Database;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace ECommerceApp.Shared.TestInfrastructure
{
    public class BaseTest<T> : CustomWebApplicationFactory<Startup>, IDisposable where T : class
    {
        protected readonly string PROPER_CUSTOMER_ID = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";
        protected readonly T _service;

        protected BaseTest(ITestOutputHelper output)
        {
            Sink.SetOutput(output);
            _service = Services.GetRequiredService<T>();
        }

        public new virtual void Dispose()
        {
            Sink.SetOutput(null);
            base.Dispose();
        }

        protected void SetHttpContextUserId(string userId)
        {
            var httpContextAccessor = Services.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
            var claims = GetUserClaims(httpContextAccessor.HttpContext.User);
            var userIdClaim = httpContextAccessor.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim != null)
            {
                claims.Remove(userIdClaim);
            }

            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            httpContextAccessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        protected void SetUserRole(string role)
        {
            var httpContextAccessor = Services.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
            var claims = GetUserClaims(httpContextAccessor.HttpContext.User);
            var userIdClaim = httpContextAccessor.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            if (userIdClaim != null)
            {
                claims.Remove(userIdClaim);
            }

            claims.Add(new Claim(ClaimTypes.Role, role));
            httpContextAccessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        private static List<Claim> GetUserClaims(ClaimsPrincipal user)
        {
            var claims = new List<Claim>();
            foreach (var claim in user.Claims)
            {
                claims.Add(new Claim(claim.Type, claim.Value));
            }
            return claims;
        }

        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessorTest>();
        }

        protected TService GetRequiredService<TService>()
        {
            return Services.GetRequiredService<TService>();
        }
    }
}

