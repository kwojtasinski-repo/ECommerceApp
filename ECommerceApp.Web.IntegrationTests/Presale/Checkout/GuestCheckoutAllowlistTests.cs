using ECommerceApp.Web.Areas.AccountProfile.Controllers;
using ECommerceApp.Web.Areas.Presale.Controllers;
using SalesOrdersController = ECommerceApp.Web.Areas.Sales.Controllers.OrdersController;
using SalesPaymentsController = ECommerceApp.Web.Areas.Sales.Controllers.PaymentsController;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Presale.Checkout
{
    /// <summary>
    /// ADR-0030 §12 — the anonymous CheckoutController surface is a closed list.
    /// Keep this list aligned with the ADR when a guest-reachable action is intentionally added.
    /// </summary>
    public sealed class GuestCheckoutAllowlistTests
    {
        private static readonly IReadOnlySet<string> ExpectedAnonymousActions =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Cart",
                "PlaceOrder",
                "CheckoutStatus",
                "CancelCheckout",
                "Summary",
                "Order",
                "RequestOrderAccess",
                "ConfirmOrderAccess",
                "CreateAccount",
                "AddToCart"
            };

        [Fact]
        public void CheckoutController_AnonymousActions_MatchAdr0030Section12Allowlist()
        {
            var anonymousActions = typeof(CheckoutController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(IsAction)
                .Where(method => method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
                .Select(method => method.Name)
                .ToHashSet(StringComparer.Ordinal);

            anonymousActions.ShouldBe(ExpectedAnonymousActions,
                "ADR-0030 §12 defines the closed anonymous checkout surface; review any intentional route addition there first");
        }

        [Fact]
        public void CheckoutController_NonAllowlistedActions_RequireAuthentication()
        {
            var actions = typeof(CheckoutController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(IsAction)
                .ToList();

            foreach (var action in actions)
            {
                var isAnonymous = action.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
                if (!ExpectedAnonymousActions.Contains(action.Name))
                {
                    isAnonymous.ShouldBeFalse($"CheckoutController.{action.Name} is outside ADR-0030 §12");
                }
            }
        }

        /// <summary>
        /// ADR-0030 §12 explicitly calls out AccountProfile as an area that must never gain an
        /// anonymous action as a side effect of the guest-checkout work. This is a narrower guard
        /// than a full-app route enumeration (see the plan's "Risks" section — reflection across every
        /// controller in the solution was left as an implementation-time choice), scoped to exactly the
        /// areas the ADR names by name plus the two ACLs Phase 7 added outside CheckoutController.
        /// Pre-existing, unrelated-to-ADR-0030 anonymous surfaces (HomeController, StorefrontController,
        /// the public-image ImagesController, etc.) are intentionally not asserted against here.
        /// </summary>
        [Fact]
        public void ProfileController_HasNoAnonymousActions()
        {
            typeof(ProfileController).IsDefined(typeof(AllowAnonymousAttribute), inherit: true)
                .ShouldBeFalse("AccountProfile must remain fully authentication-gated per ADR-0030 §12");

            var anonymousActions = typeof(ProfileController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(IsAction)
                .Where(method => method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
                .Select(method => method.Name)
                .ToList();

            anonymousActions.ShouldBeEmpty(
                "ADR-0030 §12: 'nothing else gains [AllowAnonymous] as a side effect of this work, including AccountProfile'");
        }

        /// <summary>
        /// Phase 9 (ADR-0030 §11 revision) replaced the earlier <c>[AllowAnonymous]</c>-plus-manual-cookie
        /// mechanism on <c>PaymentsController.Create</c>/<c>OrdersController.Details</c> with bare
        /// <c>[Authorize]</c> — the class-level default policy accepts either <c>Identity.Application</c>
        /// or the new <c>GuestAccess</c> scheme, and ownership (including the single-order blast-radius
        /// limit for guests) is enforced once, resource-based, via <c>IOrderAccessAuthorizer</c> inside the
        /// action. So neither controller should carry <c>[AllowAnonymous]</c> at all any more — that guard
        /// is still worth keeping, since its reappearance would mean a caller bypassed authentication
        /// entirely, not just the ownership check.
        /// </summary>
        [Fact]
        public void SalesPaymentsController_HasNoAnonymousActions()
        {
            var anonymousActions = typeof(SalesPaymentsController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(IsAction)
                .Where(method => method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
                .Select(method => method.Name)
                .ToList();

            anonymousActions.ShouldBeEmpty(
                "ADR-0030 §11 revision: guest access to Payments.Create goes through the GuestAccess " +
                "authentication scheme + OrderAccess policy, not [AllowAnonymous]");
        }

        [Fact]
        public void SalesOrdersController_HasNoAnonymousActions()
        {
            var anonymousActions = typeof(SalesOrdersController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(IsAction)
                .Where(method => method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
                .Select(method => method.Name)
                .ToList();

            anonymousActions.ShouldBeEmpty(
                "ADR-0030 §11 revision: guest access to Orders.Details goes through the GuestAccess " +
                "authentication scheme + OrderAccess policy, not [AllowAnonymous]");
        }

        /// <summary>
        /// The narrower, positive half of the invariant above: <c>Create</c>/<c>Details</c> must not gain
        /// a restrictive <c>[Authorize(Roles = ...)]</c>/<c>[Authorize(Policy = ...)]</c> override either —
        /// that would silently exclude the <c>GuestAccess</c> scheme (which only satisfies the class-level
        /// default policy) and re-lock guests out, defeating the phase without tripping the "no
        /// AllowAnonymous" guard above.
        /// </summary>
        [Fact]
        public void SalesPaymentsController_Create_UsesClassLevelDefaultAuthorization()
        {
            var createMethods = typeof(SalesPaymentsController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(IsAction)
                .Where(method => method.Name == "Create");

            foreach (var method in createMethods)
            {
                method.IsDefined(typeof(AuthorizeAttribute), inherit: false).ShouldBeFalse(
                    "Payments.Create must rely on the controller's class-level [Authorize] default policy " +
                    "(accepts GuestAccess or Identity.Application), not a narrower per-action override");
            }
        }

        [Fact]
        public void SalesOrdersController_Details_UsesClassLevelDefaultAuthorization()
        {
            var detailsMethod = typeof(SalesOrdersController)
                .GetMethod("Details", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            detailsMethod.IsDefined(typeof(AuthorizeAttribute), inherit: false).ShouldBeFalse(
                "Orders.Details must rely on the controller's class-level [Authorize] default policy " +
                "(accepts GuestAccess or Identity.Application), not a narrower per-action override");
        }

        private static bool IsAction(MethodInfo method) =>
            method.GetCustomAttributes(inherit: true).Any(attribute =>
                attribute is HttpGetAttribute or HttpPostAttribute or HttpPutAttribute or
                HttpDeleteAttribute or HttpPatchAttribute or AcceptVerbsAttribute);
    }
}
