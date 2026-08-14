using ECommerceApp.Web.Areas.Presale;
using ECommerceApp.Web.Areas.Presale.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;
using AwesomeAssertions;

namespace ECommerceApp.UnitTests.Web
{
    public class ShopperIdentityResolverTests
    {
        [Fact]
        public void Resolve_Authenticated_ReturnsClaimUserId()
        {
            var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "user-42") }, "test")));

            var result = new ShopperIdentityResolver().Resolve(context);

            result.Value.Should().Be("user-42");
            context.Response.Headers.ContainsKey("Set-Cookie").Should().BeFalse();
        }

        [Fact]
        public void Resolve_NoCookieNoAuth_MintsNewCookieAndReturnsToken()
        {
            var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()));

            var result = new ShopperIdentityResolver().Resolve(context);

            result.Value.Should().StartWith("gst_");
            context.Response.Headers["Set-Cookie"].ToString().Should().Contain(GuestSession.CookieName);
            context.Response.Headers["Set-Cookie"].ToString().Should().Contain("httponly");
            context.Response.Headers["Set-Cookie"].ToString().Should().Contain("samesite=lax");
            context.Response.Headers["Set-Cookie"].ToString().Should().Contain("secure");
        }

        [Fact]
        public void Resolve_ExistingGuestCookie_ReturnsSameToken()
        {
            var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()));
            context.Request.Headers.Cookie = $"{GuestSession.CookieName}=gst_existing";

            var result = new ShopperIdentityResolver().Resolve(context);

            result.Value.Should().Be("gst_existing");
            context.Response.Headers.ContainsKey("Set-Cookie").Should().BeFalse();
        }

        private static DefaultHttpContext CreateContext(ClaimsPrincipal user)
        {
            return new DefaultHttpContext { User = user };
        }
    }
}