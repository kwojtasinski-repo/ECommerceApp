using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace ECommerceApp.Web.Areas.Presale.Authorization
{
    /// <summary>
    /// The app-wide authorization policy setup, extracted out of <c>Startup.ConfigureServices</c> so it
    /// can be built in isolation by a fast unit test (<c>AppAuthorizationPoliciesTests</c>) instead of
    /// only being provable through a full HTTP-hosted integration test. This is a real security
    /// boundary — see <see cref="CustomerOrGuestPolicy"/> — so a regression here (DefaultPolicy widened
    /// back to include GuestAccess, or the named policy losing a scheme) should fail in milliseconds,
    /// not only when someone happens to hit the affected route in an integration test.
    /// </summary>
    public static class AppAuthorizationPolicies
    {
        public const string CustomerOrGuestPolicy = "CustomerOrGuest";
        public const string OrderAccessPolicy = "OrderAccess";

        public static void Configure(AuthorizationOptions options)
        {
            // Deliberately NOT widened to include GuestAccess: this is the app-wide default for every
            // bare [Authorize] (every Razor Pages area/folder convention too, notably Identity/Manage
            // via AddDefaultIdentity) — a GuestAccess ticket is not a real ApplicationUser, so it must
            // stay out of anything that doesn't explicitly opt in.
            options.DefaultPolicy = new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(OrderAccessPolicy, policy =>
                policy.Requirements.Add(new OrderAccessRequirement()));

            // The explicit opt-in for the handful of controllers (checkout, orders, payments, refunds,
            // order items) that must accept an anonymous-turned-GuestAccess caller as well as a real
            // signed-in customer. Everything else in the app stays ApplicationScheme-only by default —
            // see DefaultPolicy above.
            options.AddPolicy(CustomerOrGuestPolicy, policy =>
                policy.AddAuthenticationSchemes(
                        IdentityConstants.ApplicationScheme,
                        GuestAccessDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser());
        }
    }
}
