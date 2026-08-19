using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Web.Areas.Presale.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace ECommerceApp.UnitTests.Web
{
    public class GuestAccessPrincipalValidatorTests
    {
        private const int OrderId = 7;
        private const string GuestUserId = "gst_42";
        private const string BackingToken = "oat_abc123";

        [Fact]
        public async Task ValidatedWithinInterval_SkipsReCheck_DoesNotRejectOrRenew()
        {
            var orderAccessService = new Mock<IOrderAccessService>(MockBehavior.Strict);
            var context = BuildContext(DateTimeOffset.UtcNow - GuestAccessPrincipalValidator.RevalidationInterval + TimeSpan.FromMinutes(1));

            await new GuestAccessPrincipalValidator(orderAccessService.Object).ValidateAsync(context);

            context.ShouldRenew.Should().BeFalse();
            context.Principal.Should().NotBeNull("a still-fresh validation must not touch the principal at all");
            orderAccessService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ValidationExpired_BackingTokenStillValid_RenewsAndRefreshesTimestamp()
        {
            var orderAccessService = new Mock<IOrderAccessService>();
            orderAccessService
                .Setup(s => s.HasAccessAsync(OrderId, BackingToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var context = BuildContext(DateTimeOffset.UtcNow - GuestAccessPrincipalValidator.RevalidationInterval - TimeSpan.FromMinutes(1));

            await new GuestAccessPrincipalValidator(orderAccessService.Object).ValidateAsync(context);

            context.ShouldRenew.Should().BeTrue();
            context.Principal.Should().NotBeNull();
            var identity = (ClaimsIdentity)context.Principal!.Identity!;
            var refreshedClaim = identity.FindFirst(GuestAccessDefaults.ValidatedAtClaim);
            refreshedClaim.Should().NotBeNull();
            DateTimeOffset.Parse(refreshedClaim!.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// The actual payoff test: once the 5-minute cache window has elapsed, a GuestAccess cookie
        /// whose backing OrderAccessToken row was deleted (revoked — e.g. by an admin, or a future
        /// revocation feature) must stop granting access, not silently keep working until the cookie's
        /// own 30-day expiry.
        /// </summary>
        [Fact]
        public async Task ValidationExpired_BackingTokenRevoked_RejectsPrincipal()
        {
            var orderAccessService = new Mock<IOrderAccessService>();
            orderAccessService
                .Setup(s => s.HasAccessAsync(OrderId, BackingToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var context = BuildContext(DateTimeOffset.UtcNow - GuestAccessPrincipalValidator.RevalidationInterval - TimeSpan.FromMinutes(1));

            await new GuestAccessPrincipalValidator(orderAccessService.Object).ValidateAsync(context);

            context.Principal.Should().BeNull("RejectPrincipal must clear the principal once the backing token is gone");
            context.ShouldRenew.Should().BeFalse();
        }

        [Fact]
        public async Task MissingOrderIdOrTokenClaim_RejectsPrincipal_WithoutCallingOrderAccessService()
        {
            var orderAccessService = new Mock<IOrderAccessService>(MockBehavior.Strict);
            var context = BuildContext(
                DateTimeOffset.UtcNow - GuestAccessPrincipalValidator.RevalidationInterval - TimeSpan.FromMinutes(1),
                includeOrderIdClaim: false);

            await new GuestAccessPrincipalValidator(orderAccessService.Object).ValidateAsync(context);

            context.Principal.Should().BeNull();
            orderAccessService.VerifyNoOtherCalls();
        }

        private static CookieValidatePrincipalContext BuildContext(DateTimeOffset validatedAt, bool includeOrderIdClaim = true)
        {
            var claims = new System.Collections.Generic.List<Claim>
            {
                new(ClaimTypes.NameIdentifier, GuestUserId),
                new(GuestAccessDefaults.BackingTokenClaim, BackingToken),
                new(GuestAccessDefaults.ValidatedAtClaim, validatedAt.ToString("O", CultureInfo.InvariantCulture))
            };
            if (includeOrderIdClaim)
            {
                claims.Add(new Claim(GuestAccessDefaults.OrderIdClaim, OrderId.ToString()));
            }

            var identity = new ClaimsIdentity(claims, GuestAccessDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, GuestAccessDefaults.AuthenticationScheme);
            var scheme = new AuthenticationScheme(
                GuestAccessDefaults.AuthenticationScheme, GuestAccessDefaults.AuthenticationScheme, typeof(CookieAuthenticationHandler));

            return new CookieValidatePrincipalContext(new DefaultHttpContext(), scheme, new CookieAuthenticationOptions(), ticket);
        }
    }
}
