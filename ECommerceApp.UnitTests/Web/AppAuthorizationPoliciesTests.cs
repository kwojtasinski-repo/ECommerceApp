using System;
using System.Reflection;
using AwesomeAssertions;
using ECommerceApp.Web.Areas.Presale.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECommerceApp.UnitTests.Web
{
    /// <summary>
    /// Fast, in-process proof of the boundary between a real registered account and a
    /// GuestAccess-authenticated guest — see <see cref="AppAuthorizationPolicies"/> for the full
    /// rationale. Builds <see cref="AuthorizationOptions"/> through the exact same
    /// <see cref="AppAuthorizationPolicies.Configure"/> method <c>Startup.cs</c> wires up (no
    /// duplicated/drifting copy of the policy setup), so a regression here — <c>DefaultPolicy</c>
    /// widened back to admit GuestAccess, <c>CustomerOrGuest</c> losing a scheme, or a controller
    /// silently gaining/losing the guest opt-in — fails in milliseconds instead of only surfacing
    /// through an HTTP-hosted integration test.
    /// </summary>
    public class AppAuthorizationPoliciesTests
    {
        [Fact]
        public void DefaultPolicy_OnlyAcceptsRealApplicationScheme_NotGuestAccess()
        {
            var options = BuildOptions();

            options.DefaultPolicy.AuthenticationSchemes.Should().ContainSingle()
                .Which.Should().Be(IdentityConstants.ApplicationScheme,
                    "every bare [Authorize] in the app — including Identity/Manage — must stay closed to a GuestAccess ticket by default");
        }

        [Fact]
        public void CustomerOrGuestPolicy_AcceptsBothRealAccountsAndGuestAccess()
        {
            var options = BuildOptions();

            var policy = options.GetPolicy(AppAuthorizationPolicies.CustomerOrGuestPolicy);

            policy.Should().NotBeNull();
            policy!.AuthenticationSchemes.Should().Contain(IdentityConstants.ApplicationScheme);
            policy.AuthenticationSchemes.Should().Contain(GuestAccessDefaults.AuthenticationScheme);
        }

        [Fact]
        public void OrderAccessPolicy_IsRegistered()
        {
            var options = BuildOptions();

            options.GetPolicy(AppAuthorizationPolicies.OrderAccessPolicy).Should().NotBeNull();
        }

        [Theory]
        [InlineData(typeof(ECommerceApp.Web.Areas.Presale.Controllers.CheckoutController))]
        [InlineData(typeof(ECommerceApp.Web.Areas.Sales.Controllers.OrdersController))]
        [InlineData(typeof(ECommerceApp.Web.Areas.Sales.Controllers.PaymentsController))]
        [InlineData(typeof(ECommerceApp.Web.Areas.Sales.Controllers.RefundController))]
        [InlineData(typeof(ECommerceApp.Web.Areas.Sales.Controllers.OrderItemsController))]
        public void GuestFacingControllers_UseCustomerOrGuestPolicyAtClassLevel(Type controllerType)
        {
            var attribute = controllerType.GetCustomAttribute<AuthorizeAttribute>(inherit: false);

            attribute.Should().NotBeNull($"{controllerType.Name} must carry a class-level [Authorize]");
            attribute!.Policy.Should().Be(AppAuthorizationPolicies.CustomerOrGuestPolicy,
                $"{controllerType.Name} must explicitly opt in to GuestAccess via the named policy, not rely on a widened DefaultPolicy");
        }

        /// <summary>
        /// The negative half of the invariant above: AccountProfile is not part of ADR-0030's guest
        /// surface and must stay on the plain ApplicationScheme-only DefaultPolicy. Guards against
        /// someone "fixing" a future access issue there by copy-pasting the CustomerOrGuest policy.
        /// </summary>
        [Fact]
        public void ProfileController_UsesBareAuthorize_NotCustomerOrGuest()
        {
            var attribute = typeof(ECommerceApp.Web.Areas.AccountProfile.Controllers.ProfileController)
                .GetCustomAttribute<AuthorizeAttribute>(inherit: false);

            attribute.Should().NotBeNull();
            attribute!.Policy.Should().BeNull(
                "AccountProfile must remain fully authentication-gated to real accounts only (ADR-0030 §12) — never opted into GuestAccess");
        }

        private static AuthorizationOptions BuildOptions()
        {
            var services = new ServiceCollection();
            services.AddAuthorization(AppAuthorizationPolicies.Configure);
            return services.BuildServiceProvider().GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        }
    }
}
